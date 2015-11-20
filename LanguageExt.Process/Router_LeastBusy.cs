﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static LanguageExt.Process;
using static LanguageExt.Prelude;

namespace LanguageExt
{
    public static partial class Router
    {
        /// <summary>
        /// Spawns a new process with Count worker processes, each message is 
        /// sent to the least busy worker.
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <returns>Process ID of the delegator process</returns>
        public static ProcessId leastBusy<S, T>(
            ProcessName Name,
            int Count,
            Func<S> Setup,
            Func<S, T, S> Inbox,
            ProcessFlags Flags                    = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize                    = ProcessSetting.DefaultMailboxSize,
            string WorkerName                     = "worker"
            )
        {
            if (Inbox == null) throw new ArgumentNullException(nameof(Inbox));
            if (Setup == null) throw new ArgumentNullException(nameof(Setup));
            if (Count < 1) throw new ArgumentException($"{nameof(Count)} should be greater than 0");

            return spawn<Unit, T>(
                Name,
                () =>
                {
                    spawnMany(Count, WorkerName, Setup, Inbox, Flags);
                    return unit;
                },
                (_, msg) =>
                {
                    var disps = Children.Map(c => Tuple(c, ActorContext.GetDispatcher(c)))
                                        .Values
                                        .OrderBy(c => c.Item2.GetInboxCount())
                                        .ToList();

                    fwd(disps.First().Item1, msg);
                    return unit;
                },
                Flags, 
                Strategy, 
                MaxMailboxSize
            );
        }

        /// <summary>
        /// Spawns a new process with that routes each message to the 
        /// least busy worker.
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <returns>Process ID of the delegator process</returns>
        public static ProcessId leastBusy<T>(
            ProcessName Name,
            IEnumerable<ProcessId> Workers,
            bool RemoveWorkerWhenTerminated = true,
            ProcessFlags Flags = ProcessFlags.Default,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize
            )
        {
            if (Workers == null) throw new ArgumentNullException(nameof(Workers));
            var workers = Set.createRange(Workers);
            if (workers.Count < 1) throw new ArgumentException($"{nameof(Workers)} should have a length of at least 1");

            var router = spawn<T>(
                Name,
                msg =>
                {
                    var disps = workers.Map(c => Tuple(c, ActorContext.GetDispatcher(c)))
                                       .OrderBy(c => c.Item2.GetInboxCount())
                                       .ToList();

                    fwd(disps.First().Item1, msg);
                },
                Flags,
                DefaultStrategy,
                MaxMailboxSize,
                Terminated: pid => workers = workers.Remove(pid)
            );

            if (RemoveWorkerWhenTerminated)
            {
                workers.Iter(w => watch(router, w));
            }

            return router;
        }

        /// <summary>
        /// Spawns a new process with Count worker processes, each message is mapped
        /// and sent to the least busy worker
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <returns>Process ID of the delegator process</returns>
        public static ProcessId leastBusyMap<S, T, U>(
            ProcessName Name,
            int Count,
            Func<S> Setup,
            Func<S, U, S> Inbox,
            Func<T, U> Map,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker"
            )
        {
            if (Inbox == null) throw new ArgumentNullException(nameof(Inbox));
            if (Setup == null) throw new ArgumentNullException(nameof(Setup));
            if (Count < 1) throw new ArgumentException($"{nameof(Count)} should be greater than 0");

            return spawn<Unit, T>(
                Name,
                () =>
                {
                    spawnMany(Count, WorkerName, Setup, Inbox, Flags);
                    return unit;
                },
                (_, msg) =>
                {
                    var umsg = Map(msg);

                    var disps = Children.Map(c => Tuple(c, ActorContext.GetDispatcher(c)))
                                        .Values
                                        .OrderBy(c => c.Item2.GetInboxCount())
                                        .ToList();

                    return fwd(disps.First().Item1, umsg);
                },
                Flags,
                Strategy,
                MaxMailboxSize
            );
        }

        /// <summary>
        /// Spawns a new process with that routes each message is mapped and 
        /// sent to the least busy worker
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <returns>Process ID of the delegator process</returns>
        public static ProcessId leastBusyMap<T, U>(
            ProcessName Name,
            IEnumerable<ProcessId> Workers,
            Func<T, U> Map,
            bool RemoveWorkerWhenTerminated = true,
            ProcessFlags Flags = ProcessFlags.Default,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize
            )
        {
            if (Workers == null) throw new ArgumentNullException(nameof(Workers));
            var workers = Set.createRange(Workers);
            if (workers.Count < 1) throw new ArgumentException($"{nameof(Workers)} should have a length of at least 1");
            var router = spawn<T>(
                Name,
                msg =>
                {
                    var umsg = Map(msg);
                    var disps = workers.Map(c => Tuple(c, ActorContext.GetDispatcher(c)))
                                       .OrderBy(c => c.Item2.GetInboxCount())
                                       .ToList();

                    fwd(disps.First().Item1, msg);
                },
                Flags,
                DefaultStrategy,
                MaxMailboxSize,
                Terminated: pid => workers = workers.Remove(pid)
            );

            if (RemoveWorkerWhenTerminated)
            {
                workers.Iter(w => watch(router, w));
            }

            return router;
        }

        /// <summary>
        /// Spawns a new process with Count worker processes, each message is 
        /// sent to the least busy worker.
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <returns>Process ID of the delegator process</returns>
        public static ProcessId leastBusy<T>(
            ProcessName Name,
            int Count,
            Action<T> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker"
            ) =>
            leastBusy<Unit, T>(Name, Count, () => unit, (_, msg) => { Inbox(msg); return unit; }, Flags, Strategy, MaxMailboxSize, WorkerName);

        /// <summary>
        /// Spawns a new process with Count worker processes, each message is mapped
        /// and sent to the least busy worker.
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <returns>Process ID of the delegator process</returns>
        public static ProcessId leastBusyMap<T, U>(
            ProcessName Name,
            int Count,
            Action<U> Inbox,
            Func<T, U> Map,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker"
            ) =>
            leastBusyMap(Name, Count, () => unit, (_, umsg) => { Inbox(umsg); return unit; }, Map, Flags, Strategy, MaxMailboxSize, WorkerName);
    }
}
