﻿using System;
using System.Diagnostics;
using Spreads.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads {
    public class SortedMapGenerator<V> {
        private readonly long _periodTicks;
        private readonly Func<int, V, V> _valueGenerator;
        private Stopwatch sw = new Stopwatch();
        private CancellationTokenSource Cts;
        private Task runner;
        /// <summary>
        /// Generate T every number of ticks with counter
        /// </summary>
        /// <param name="periodTicks"></param>
        /// <param name="valueGenerator"></param>
        public SortedMapGenerator(long periodTicks, Func<int, V, V> valueGenerator) {
            _periodTicks = periodTicks;
            _valueGenerator = valueGenerator;
        }

        /// <summary>
        /// Returns a sorted map that is being filled by new values
        /// </summary>
        /// <param name="existing"></param>
        /// <returns></returns>
        public SortedMap<DateTime, V> Generate(int durationMsecs = 0, SortedMap<DateTime, V> existing = null, CancellationTokenSource cts = null) {
            var spin = new SpinWait();
            var sm = existing ?? new SortedMap<DateTime, V>();
            Cts = cts ?? new CancellationTokenSource();
            var c = 0;
            var previous = sm.IsEmpty ? default(V) : sm.Last.Value;
            if (durationMsecs > 0) {
                Cts.CancelAfter(durationMsecs);
            }
            var startDt = DateTime.UtcNow;

            sw.Start();

            runner = Task.Run(() => {
                while (!Cts.IsCancellationRequested) {
                    while (sw.Elapsed.Ticks < _periodTicks+1) {
                        // spin
                        //Console.WriteLine("Spin");
                    }
                    // do work
                    var next = _valueGenerator(c, previous);
                    sm.Add(startDt.AddTicks(c * _periodTicks + sw.ElapsedTicks), next);
                    previous = next;
                    c++;
                    sw.Restart();
                }

            }, Cts.Token);

            return sm;

        }

        public void Cancel() {
            Cts.Cancel();
        }

    }
}
