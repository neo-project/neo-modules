using Neo.Consensus;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    partial class PerformanceMonitor
    {
        /// <summary>
        /// Process "commit time" command
        /// Prints the time in milliseconds to commit in the network
        /// </summary>
        private bool OnCommitTimeCommand()
        {
            var time = GetTimeToCommit(true);
            if (time > 0)
            {
                Console.WriteLine($"Time to commit: {time:0.##} milliseconds");
            }
            else
            {
                Console.WriteLine("Timeout");
            }

            return true;
        }

        /// <summary>
        /// Calculates the time difference between the persist and the commit
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        /// <returns>
        /// Returns the difference between the persist time and the commit time
        /// </returns>
        private double GetTimeToCommit(bool printMessages = false)
        {
            var commit = new TaskCompletionSource<bool>();
            var persist = new TaskCompletionSource<bool>();

            DateTime persistTime = DateTime.Now;
            DateTime commitTime = persistTime;

            CommitHandler commitAction = (store) =>
            {
                commitTime = DateTime.Now;
                commit?.TrySetResult(true);
            };
            PersistHandler persistAction = (store, app) =>
            {
                persistTime = DateTime.Now;
                persist?.TrySetResult(true);
            };

            OnCommitEvent += commitAction;
            OnPersistEvent += persistAction;

            if (printMessages)
            {
                Console.WriteLine("Waiting for the next commit...");
            }

            List<Task> tasks = new List<Task>
            {
                commit.Task,
                persist.Task
            };

            var millisecondsToTimeOut = (int)Blockchain.MillisecondsPerBlock;
            Task.WaitAll(tasks.ToArray(), millisecondsToTimeOut);

            OnCommitEvent -= commitAction;
            OnPersistEvent -= persistAction;

            if (!tasks.TrueForAll(task => !task.IsCanceled))
            {
                return 0;
            }

            var timeToCommit = persistTime - commitTime;

            return Math.Abs(timeToCommit.TotalMilliseconds);
        }

        /// <summary>
        /// Process "confirmation time" command
        /// Prints the time in milliseconds to confirm the block
        /// </summary>
        private bool OnConfirmationTimeCommand()
        {
            var time = GetTimeToConfirm(true);

            if (time > 0)
            {
                Console.WriteLine($"Confirmation Time: {time:0.##} milliseconds");
            }
            else
            {
                Console.WriteLine("Timeout. Make sure the start consensus command has been run.");
            }

            return true;
        }

        /// <summary>
        /// Calculates the time difference between the commit request and the actual commit
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        /// <returns>
        /// Returns the difference between the commit request time and the actual commit time
        /// </returns>
        private double GetTimeToConfirm(bool printMessages = false)
        {
            var commit = new TaskCompletionSource<bool>(false);
            var consensusMessage = new TaskCompletionSource<bool>(false);

            DateTime consensusMessageTime = DateTime.Now;
            DateTime commitTime = DateTime.Now;

            CommitHandler commitAction = (store) =>
            {
                commitTime = DateTime.Now;
                commit?.TrySetResult(true);
            };
            ConsensusMessageHandler consensusMessageAction = (payload) =>
            {
                if (payload.ConsensusMessage is Commit)
                {
                    consensusMessageTime = DateTime.Now;
                    consensusMessage?.TrySetResult(true);
                }
            };

            OnCommitEvent += commitAction;
            OnConsensusMessageEvent += consensusMessageAction;

            if (printMessages)
            {
                Console.WriteLine("Waiting for the next commit...");
            }

            List<Task<bool>> tasks = new List<Task<bool>>
            {
                commit.Task,
                consensusMessage.Task
            };

            var millisecondsToTimeOut = (int)Blockchain.MillisecondsPerBlock;
            Task.WaitAll(tasks.ToArray(), millisecondsToTimeOut);

            OnCommitEvent -= commitAction;
            OnConsensusMessageEvent -= consensusMessageAction;

            foreach (Task<bool> task in tasks)
            {
                if (!task.IsCompleted)
                {
                    return 0;
                }
            }

            var timeToConfirm = commitTime - consensusMessageTime;

            return Math.Abs(timeToConfirm.TotalMilliseconds);
        }

        /// <summary>
        /// Process "payload time" command
        /// Prints the time in milliseconds to receive a payload
        /// </summary>
        private bool OnPayloadTimeCommand()
        {
            var time = GetPayloadTime(true);
            if (time > 0)
            {
                Console.WriteLine($"Payload Time: {time:0.##} milliseconds");
            }
            else
            {
                Console.WriteLine("Timeout. Make sure the start consensus command has been run.");
            }

            return true;
        }

        /// <summary>
        /// Calculates the time difference between the payload send and receive time
        /// </summary>
        /// <param name="printMessages">
        /// Specifies if the messages should be printed in the console.
        /// </param>
        /// <returns>
        /// Returns the difference between the payload send time and receive time
        /// </returns>
        private double GetPayloadTime(bool printMessages = false)
        {
            var consensusMessage = new TaskCompletionSource<bool>();

            DateTime consensusMessageTime = DateTime.Now;
            DateTime consensusPayloadTime = consensusMessageTime;

            ConsensusMessageHandler consensusMessageAction = (payload) =>
            {
                var timestamp = GetPayloadTimestamp(payload);
                if (timestamp > ulong.MinValue)
                {
                    consensusMessageTime = DateTime.UtcNow;
                    consensusPayloadTime = TimestampToDateTime(timestamp);
                    consensusMessage?.TrySetResult(true);
                }
            };

            OnConsensusMessageEvent += consensusMessageAction;

            if (printMessages)
            {
                Console.WriteLine("Waiting for the next payload...");
            }
            var millisecondsToTimeOut = (int)Blockchain.MillisecondsPerBlock;
            consensusMessage.Task.Wait(millisecondsToTimeOut);

            OnConsensusMessageEvent -= consensusMessageAction;

            if (!consensusMessage.Task.IsCompleted)
            {
                return 0;
            }

            var timeToConfirm = consensusMessageTime - consensusPayloadTime;
            return Math.Abs(timeToConfirm.TotalMilliseconds);
        }

        /// <summary>
        /// Gets the timestamp from a consensus payload
        /// </summary>
        /// <param name="payload">
        /// The payload to get the timestamp
        /// </param>
        /// <returns>
        /// Returns the timestamp of the <paramref name="payload"/> if it has the Timestamp property;
        /// otherwise, returns 0.
        /// </returns>
        private ulong GetPayloadTimestamp(ConsensusPayload payload)
        {
            switch (payload.ConsensusMessage)
            {
                case ChangeView view:
                    return view.Timestamp;
                case PrepareRequest request:
                    return request.Timestamp;
                case RecoveryRequest request:
                    return request.Timestamp;
                default:
                    return ulong.MinValue;
            }
        }

        /// <summary>
        /// Converts a timestamp value to a <see cref="DateTime"/> object
        /// </summary>
        /// <param name="timestamp">
        /// THe timestamp value in milliseconds
        /// </param>
        /// <returns>
        /// If the result is a valid <see cref="DateTime"/>, returns the DateTime object that represents
        /// the timestamp value; otherwise, returns the <see cref="DateTime.UnixEpoch"/>.
        /// </returns>
        private DateTime TimestampToDateTime(ulong timestamp)
        {
            try
            {
                return DateTime.UnixEpoch.AddMilliseconds(timestamp);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.UnixEpoch;
            }
        }
    }
}
