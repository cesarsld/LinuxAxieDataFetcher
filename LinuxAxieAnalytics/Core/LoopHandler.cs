﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AxieDataFetcher.BattleData;
using AxieDataFetcher.EggsSpawnedData;
using AxieDataFetcher.BlockchainFetcher;
using AxieDataFetcher.Mongo;

namespace AxieDataFetcher.Core
{
    class LoopHandler
    {
        public static int lastUnixTimeCheck = 0;
        public static readonly int unixTimeBetweenUpdates = 86400;

        public static async Task UpdateServiceCheckLoop()
        {
            while (true)
            {
                int unixTime = Convert.ToInt32(((DateTimeOffset)(DateTime.UtcNow)).ToUnixTimeSeconds());
                if (lastUnixTimeCheck == 0) UpdateUnixLastCheck();
                Console.Clear();
                Console.WriteLine($"Time before next update :  {86400 - (unixTime - lastUnixTimeCheck)} seconds");
                if (unixTime - lastUnixTimeCheck >= unixTimeBetweenUpdates)
                {
                    //rework long term
                    //await EggsSpawnDataFetcher.GetEggsSpawnedFromCheckpoint();
                    await AxieDataGetter.FetchAllSalesData();
                    await MarketplaceDatabase.ComputeAllSales();
                    // obsolete
                    //await WinrateCollector.GetBattlesFromRange();
                    lastUnixTimeCheck = unixTime;
                    using (var tw = new StreamWriter("AxieData/LastTimeCheck.txt"))
                    {
                        tw.Write(lastUnixTimeCheck.ToString());
                    }
                }

                await Task.Delay(60000);
            }
        }

        public static void UpdateUnixLastCheck()
        {
            using (StreamReader sr = new StreamReader("AxieData/LastTimeCheck.txt", Encoding.UTF8))
            {
                lastUnixTimeCheck = Convert.ToInt32(sr.ReadToEnd());
            }
        }
    }
}
