using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HighProject.MapCore
{
    public sealed class DeterminismResult
    {
        // Checksum of one plain, single-threaded bake.
        public string ReferenceChecksum { get; set; }

        // Checksums of the bakes that ran at the same time on different threads.
        public IReadOnlyList<string> ParallelChecksums { get; set; }

        public long ElapsedMilliseconds { get; set; }

        public bool AllIdentical => ParallelChecksums.All(checksum => checksum == ReferenceChecksum);

        public override string ToString()
        {
            if (AllIdentical)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "Deterministic: 1 single-threaded bake and {0} bakes at the same time all give checksum {1} ({2} ms).",
                    ParallelChecksums.Count, ReferenceChecksum, ElapsedMilliseconds);
            }

            return string.Format(CultureInfo.InvariantCulture,
                "NOT deterministic: the single-threaded bake gave {0} but the parallel bakes gave {1}.",
                ReferenceChecksum, string.Join(", ", ParallelChecksums.Distinct()));
        }
    }

    // Bakes the same parsed file once normally and then several times at the same time on different threads, and
    // compares the checksums. Equal checksums mean the result does not depend on timing or on other bakes running.
    public static class GridDeterminismCheck
    {
        // createOptions is called once for the reference bake and once per parallel bake; it may return the same
        // options each time (they are only read) or fresh copies.
        public static DeterminismResult Run(
            OsmDocument document,
            Func<GridBuildOptions> createOptions,
            int parallelRuns = 8,
            CancellationToken cancellationToken = default)
        {
            if (parallelRuns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(parallelRuns), "At least one parallel run is needed.");
            }

            var timer = Stopwatch.StartNew();
            string reference = GridBuilder.Build(document, createOptions(), null, cancellationToken).Checksum;

            var checksums = new string[parallelRuns];
            GridBuildOptions[] options = Enumerable.Range(0, parallelRuns).Select(_ => createOptions()).ToArray();
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = parallelRuns, CancellationToken = cancellationToken };

            Parallel.For(0, parallelRuns, parallel, i =>
            {
                checksums[i] = GridBuilder.Build(document, options[i], null, cancellationToken).Checksum;
            });

            return new DeterminismResult
            {
                ReferenceChecksum = reference,
                ParallelChecksums = checksums,
                ElapsedMilliseconds = timer.ElapsedMilliseconds
            };
        }
    }
}
