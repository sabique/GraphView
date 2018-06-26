﻿
namespace GraphView.Transaction
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    internal class SingletonPartitionedVersionDb : VersionDb
    {
        private static volatile SingletonPartitionedVersionDb instance;

        private static readonly object initlock = new object();

        /// <summary>
        /// Whether the version db is in deamon mode
        /// In deamon mode, for every partition, it will have a thread to flush
        /// In non-daemon mode, the executor will flush requests
        /// </summary>
        internal bool DaemonMode { get; set; } = true;

        internal bool Active { get; set; } = false;

        internal long FlushWaitTicks { get; set; } = 0L;

        /// <summary>
        /// The transaction table map, txId => txTableEntry
        /// </summary>
        private Dictionary<long, TxTableEntry>[] txTable;

        private SingletonPartitionedVersionDb(int partitionCount, bool daemonMode)
            : base(partitionCount)
        {
            this.txTable = new Dictionary<long, TxTableEntry>[partitionCount];

            for (int pid = 0; pid < partitionCount; pid++)
            {
                this.txTable[pid] = new Dictionary<long, TxTableEntry>(100);
                this.dbVisitors[pid] = new SingletonPartitionedVersionDbVisitor(this.txTable[pid]);
            }

            this.PhysicalPartitionByKey = key => Math.Abs(key.GetHashCode()) % this.PartitionCount;
            this.PhysicalTxPartitionByKey = key => (int)((long)key / TxRange.range);

            this.DaemonMode = daemonMode;
        }

        public void StartDaemonThreads()
        {
            if (!this.DaemonMode)
            {
                return;
            }

            this.Active = true;
            for (int pk = 0; pk < this.PartitionCount; pk++)
            {
                Thread thread = new Thread(this.Monitor);
                thread.Start(pk);
            }
        }

        /// <summary>
        /// Only for benchmark test
        /// 
        /// Keep the number of records as the same and add new partitions to the given expectedPartitionCount
        /// That means add new visitors and dicts, finally reshuffle all data into the right partitions
        /// </summary>
        /// <param name="part"></param>
        public void ExtendPartition(int expectedPartitionCount)
        {
            int prePartitionCount = this.PartitionCount;
            this.PartitionCount = expectedPartitionCount;

            // Resize Visitors and Dicts
            Array.Resize(ref this.txTable, expectedPartitionCount);
            for (int pk = prePartitionCount; pk < expectedPartitionCount; pk++)
            {
                this.txTable[pk] = new Dictionary<long, TxTableEntry>(100);
            }

            Array.Resize(ref this.dbVisitors, expectedPartitionCount);
            for (int pk = prePartitionCount; pk < expectedPartitionCount; pk++)
            {
                this.dbVisitors[pk] = new SingletonPartitionedVersionDbVisitor(this.txTable[pk]);
            }

            Array.Resize(ref this.visitTicks, expectedPartitionCount);

            // expend partitions for version table
            ((SingletonPartitionedVersionTable)this.versionTables["ycsb_table"]).ExtendPartition(expectedPartitionCount);
        }

        /// <summary>
        /// The method to get the version db's singleton instance
        /// </summary>
        /// <param name="partitionCount">The number of partitions</param>
        /// <returns></returns>
        internal static SingletonPartitionedVersionDb Instance(int partitionCount = 4, bool daemonMode = false)
        {
            if (SingletonPartitionedVersionDb.instance == null)
            {
                lock (initlock)
                {
                    if (SingletonPartitionedVersionDb.instance == null)
                    {
                        SingletonPartitionedVersionDb.instance = new SingletonPartitionedVersionDb(partitionCount, daemonMode);
                    }
                }
            }
            return SingletonPartitionedVersionDb.instance;
        }

        internal override void Clear()
        {
            // Clear contents of the version table at first
            foreach (string key in this.versionTables.Keys)
            {
                this.versionTables[key].Clear();
            }
            this.versionTables.Clear();

            for (int pid = 0; pid < this.PartitionCount; pid++)
            {
                if (this.txTable[pid] != null)
                {
                    this.txTable[pid].Clear();
                }
            }
        }

        internal override VersionTable CreateVersionTable(string tableId, long redisDbIndex = 0)
        {
            if (!this.versionTables.ContainsKey(tableId))
            {
                lock (this.versionTables)
                {
                    if (!this.versionTables.ContainsKey(tableId))
                    {
                        SingletonPartitionedVersionTable vtable = new SingletonPartitionedVersionTable(this, tableId, this.PartitionCount);
                        this.versionTables.Add(tableId, vtable);
                    }
                }
            }

            return this.versionTables[tableId];
        }

        internal override bool DeleteTable(string tableId)
        {
            if (this.versionTables.ContainsKey(tableId))
            {
                lock (this.versionTables)
                {
                    if (this.versionTables.ContainsKey(tableId))
                    {
                        this.versionTables.Remove(tableId);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        internal override IEnumerable<string> GetAllTables()
        {
            return this.versionTables.Keys;
        }

        internal override VersionTable GetVersionTable(string tableId)
        {
            if (!this.versionTables.ContainsKey(tableId))
            {
                return null;
            }
            return this.versionTables[tableId];
        } 

        internal void Monitor(object obj)
        {
            int pk = (int)obj;
            while (this.Active)
            {
                this.Visit(VersionDb.TX_TABLE, pk);
                foreach (string tableId in this.versionTables.Keys)
                {
                    this.Visit(tableId, pk);
                }
            }
        }

        internal override void EnqueueTxEntryRequest(long txId, TxEntryRequest txEntryRequest, int execPartition = 0)
        {
            // Interlocked.Increment(ref VersionDb.EnqueuedRequests);

            // SingletonPartitionVersionDb implementation 1
            //base.EnqueueTxEntryRequest(txId, txEntryRequest, execPartition);
            //while (!txEntryRequest.Finished) ;

            // SingletonPartitionedVersionDb implementation 2
            
            int pk = this.PhysicalTxPartitionByKey(txId);
            long beginTicks = DateTime.Now.Ticks;
            this.dbVisitors[pk].Invoke(txEntryRequest);
            this.visitTicks[pk] += DateTime.Now.Ticks - beginTicks;
            // SingletonPartitionedVersionDb implementation 3
            //int pk = this.PhysicalTxPartitionByKey(txId);
            //if (pk == execPartition)
            //{
            //    this.dbVisitors[pk].Invoke(txEntryRequest);
            //}
            //else
            //{
            //    base.EnqueueTxEntryRequest(txId, txEntryRequest, execPartition);
            //}
        }
    }
}
