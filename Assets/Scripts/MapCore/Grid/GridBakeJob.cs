using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HighProject.MapCore
{
    public enum BakeJobState
    {
        Running,
        Succeeded,
        Failed,
        Canceled
    }

    // Reads an OSM file and builds the grid on a worker thread. The owner (the editor window) polls State,
    // Progress and Phase from its own thread, so no Unity API is ever called from the worker.
    // Unity types are not used here, so the job can be tested without the editor.
    public sealed class GridBakeJob
    {
        // Reading the file takes this share of the progress bar; building the grid takes the rest.
        const float ReadShare = 0.15f;

        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly Task task;
        readonly Stopwatch timer = Stopwatch.StartNew();

        volatile int state = (int)BakeJobState.Running;
        volatile string phase = "Starting";
        int progressBits;

        public BakeJobState State => (BakeJobState)state;
        public string Phase => phase;
        public float Progress => BitConverter.Int32BitsToSingle(Volatile.Read(ref progressBits));

        // Set once the job has finished successfully.
        public GridBuildResult Result { get; private set; }

        // Set when the job failed.
        public Exception Error { get; private set; }

        // True if the parsed file came from the cache instead of being read again.
        public bool DocumentWasReused { get; private set; }

        public long ElapsedMilliseconds => timer.ElapsedMilliseconds;
        public bool IsFinished => State != BakeJobState.Running;

        GridBakeJob(string osmPath, GridBuildOptions options, OsmDocumentCache cache, IProgress<float> observer)
        {
            CancellationToken token = cancellation.Token;
            task = Task.Run(() => Run(osmPath, options, cache, observer, token));
        }

        // Starts the bake in the background and returns immediately. The optional observer sees every progress value
        // on the worker thread (useful for tests; the window just polls).
        public static GridBakeJob Start(string osmPath, GridBuildOptions options, OsmDocumentCache cache, IProgress<float> observer = null)
        {
            return new GridBakeJob(osmPath, options, cache, observer);
        }

        // Asks the job to stop at the next checkpoint (between stages, or while reading the file).
        public void Cancel()
        {
            cancellation.Cancel();
        }

        // Blocks until the job has finished or the time is up. Returns true if it finished.
        public bool Wait(int milliseconds)
        {
            try
            {
                return task.Wait(milliseconds);
            }
            catch (AggregateException)
            {
                return true;
            }
        }

        void Run(string osmPath, GridBuildOptions options, OsmDocumentCache cache, IProgress<float> observer, CancellationToken token)
        {
            try
            {
                OsmDocument document = cache.TryGet(osmPath);
                DocumentWasReused = document != null;

                if (document == null)
                {
                    SetPhase("Reading OSM file");
                    document = OsmReader.ReadFile(osmPath, new CallbackProgress(value => Report(value * ReadShare, observer)), token);
                    cache.Store(osmPath, document);
                }

                SetPhase("Building grid");
                Report(ReadShare, observer);
                Result = GridBuilder.Build(document, options, new CallbackProgress(value => Report(ReadShare + value * (1f - ReadShare), observer)), token);

                SetPhase("Done");
                Report(1f, observer);
                state = (int)BakeJobState.Succeeded;
            }
            catch (OperationCanceledException)
            {
                SetPhase("Canceled");
                state = (int)BakeJobState.Canceled;
            }
            catch (Exception exception)
            {
                Error = exception;
                SetPhase("Failed");
                state = (int)BakeJobState.Failed;
            }
            finally
            {
                timer.Stop();
            }
        }

        void SetPhase(string value)
        {
            phase = value;
        }

        void Report(float value, IProgress<float> observer)
        {
            Volatile.Write(ref progressBits, BitConverter.SingleToInt32Bits(value));
            observer?.Report(value);
        }

        sealed class CallbackProgress : IProgress<float>
        {
            readonly Action<float> callback;

            public CallbackProgress(Action<float> callback)
            {
                this.callback = callback;
            }

            public void Report(float value)
            {
                callback(value);
            }
        }
    }
}
