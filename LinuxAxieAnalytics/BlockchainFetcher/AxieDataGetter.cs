﻿using System;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;
using Nethereum.Web3;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json.Linq;
using AxieDataFetcher.AxieObjects;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts.CQS;
using Newtonsoft.Json;
//using Nethereum.Contracts.Extensions;
using System.Linq;
using AxieDataFetcher.Core;
using AxieDataFetcher.Mongo;
namespace AxieDataFetcher.BlockchainFetcher
{

    public class AxieDataGetter
    {

        #region ABI & contract declaration

        private static string AxieCoreContractAddress = "0xF4985070Ce32b6B1994329DF787D1aCc9a2dd9e2";
        private static string NftAddress = "0xf5b0a3efb8e8e4c201e2a935f110eaaf3ffecb8d";
        private static string AxieLabContractAddress = "0x99ff9f4257D5b6aF1400C994174EbB56336BB79F";
        private static string AxieExtraDataContract = "0x10e304a53351b272dc415ad049ad06565ebdfe34";
        //private static string AxieLandPresaleContract = "0x7a11462A2adAed5571b91e34a127E4cbF51b152c";
        private static string AxieLandPresaleContract = "0x2299a91cc0bffd8c7f71349da8ab03527b79724f";
        #endregion
        private static BigInteger lastBlockChecked = 6727713;//sale data creation
        private static BigInteger finalBlockCheck = 9351317;
        //7331816 5318756 7338212


        public static bool IsServiceOn = true;

        public static async Task<HexBigInteger> GetSafeLowGas()
        {
            var json = "";
            using (System.Net.WebClient wc = new System.Net.WebClient())
            {
                json = await wc.DownloadStringTaskAsync("https://api.axieinfinity.com/v1/gas-price");
            }
            return new HexBigInteger(BigInteger.Parse((string)JObject.Parse(json)["safeLow"]));
        }

        public static async Task<HexBigInteger> GetStandardGas()
        {
            var json = "";
            using (System.Net.WebClient wc = new System.Net.WebClient())
            {
                json = await wc.DownloadStringTaskAsync("https://api.axieinfinity.com/v1/gas-price");
            }
            return new HexBigInteger(BigInteger.Parse((string)JObject.Parse(json)["standard"]));
        }

        public static async Task BuyAxie(HexBigInteger axieId, HexBigInteger price)
        {
            var myPassword = "";
            var account = new Account(myPassword);
            var web3 = new Web3(account, "https://mainnet.infura.io");
            var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
            object[] input = new object[2];
            input[0] = NftAddress;
            input[1] = new BigInteger(18095);

            var safeLow = await GetSafeLowGas();
            //get contract
            var auctionContract = web3.Eth.GetContract(KeyGetter.GetABI("auctionABI"), AxieCoreContractAddress);

            //get payable function
            var bidFunction = auctionContract.GetFunction("bid");
            var estimateGas = await bidFunction.EstimateGasAsync(account.Address, new HexBigInteger(8000000), price, NftAddress, axieId);
            Console.WriteLine($"Bid ID {input[1]} using {estimateGas.Value} gas");
            try
            {
                var tx = await bidFunction.SendTransactionAsync(account.Address, estimateGas, safeLow, price, input[0], input[1]);
                Console.WriteLine($"TX : {tx}");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
            }
        }

        public static async Task CheckIfShouldBuy(EventLog<AuctionCreatedEvent> auction)
        {
            //1 eth is 1 000 000 000 000 000 000 wie
            var currentPrice = auction.Event.startingPrice;

            if (currentPrice < BigInteger.Parse("1 000 000 000 000 000 000"))
            { }
        }

        public static async Task FetchAuctionData()
        {
            var web3 = new Web3("https://mainnet.infura.io");
            //get contracts
            var auctionContract = web3.Eth.GetContract(KeyGetter.GetABI("auctionABI"), AxieCoreContractAddress);

            //get events
            var auctionSuccesfulEvent = auctionContract.GetEvent("AuctionSuccessful");
            var auctionCreatedEvent = auctionContract.GetEvent("AuctionCreated");
            var auctionCancelled = auctionContract.GetEvent("AuctionCancelled");

            //set block range
            var lastBlock = await GetLastBlockCheckpoint(web3);
            var firstBlock = GetInitialBlockCheckpoint(lastBlock.BlockNumber);
            while (IsServiceOn)
            {
                try
                {
                    //prepare filters
                    var auctionFilterAll = auctionSuccesfulEvent.CreateFilterInput(firstBlock, lastBlock);
                    var auctionCancelledFilterAll = auctionCancelled.CreateFilterInput(firstBlock, lastBlock);
                    var auctionCreationFilterAll = auctionCreatedEvent.CreateFilterInput(firstBlock, lastBlock);

                    //get logs from blockchain
                    var auctionLogs = await auctionSuccesfulEvent.GetAllChanges<AuctionSuccessfulEvent>(auctionFilterAll);
                    var auctionCancelledLogs = await auctionSuccesfulEvent.GetAllChanges<AuctionCancelledEvent>(auctionFilterAll);
                    var auctionCreationLogs = await auctionCreatedEvent.GetAllChanges<AuctionCreatedEvent>(auctionCreationFilterAll);

                    if (auctionCancelledLogs != null && auctionCancelledLogs.Count > 0)
                    {
                        foreach (var cancel in auctionCancelledLogs)
                        {
                            //remove from DB
                            await MarketplaceDatabase.RemoveAxie(Convert.ToInt32(cancel.Event.tokenId.ToString()));

                            //remove market trigger
                        }
                    }
                    if (auctionLogs != null && auctionLogs.Count > 0)
                    {
                        foreach (var success in auctionLogs)
                        {
                            //remove from DB
                            await MarketplaceDatabase.RemoveAxie(Convert.ToInt32(success.Event.tokenId.ToString()));

                            //remove market triggers

                        }
                    }

                    if (auctionCreationLogs != null && auctionCreationLogs.Count > 0)
                    {

                        foreach (var log in auctionCreationLogs)
                        {
                            //add to DB
                            await MarketplaceDatabase.AddNewAxie(Convert.ToInt32(log.Event.tokenId.ToString()));
                            //check if should buy now or later
                            await CheckIfShouldBuy(log);
                        }
                    }
                    await Task.Delay(60000);
                    firstBlock = new BlockParameter(new HexBigInteger(lastBlock.BlockNumber.Value + 1));
                    lastBlock = await GetLastBlockCheckpoint(web3);
                }
                catch
                {
                    IsServiceOn = false;
                }
            }
        }


        public static async Task FetchLogsSinceLastCheck()
        {
            Console.WriteLine("Pods per day init");
            var web3 = new Web3("https://mainnet.infura.io");
            //get contracts
            var auctionContract = web3.Eth.GetContract(KeyGetter.GetABI("auctionABI"), AxieCoreContractAddress);
            var getSellerInfoFunction = auctionContract.GetFunction("getAuction");
            //var labContract = web3.Eth.GetContract(KeyGetter.GetABI("labABI"), AxieLabContractAddress);
            //var landPresaleContract = web3.Eth.GetContract(KeyGetter.GetABI("landSaleABI"), AxieLandPresaleContract);
            //get events
            var auctionSuccesfulEvent = auctionContract.GetEvent("AuctionSuccessful");
            var auctionCreatedEvent = auctionContract.GetEvent("AuctionCreated");
            //var axieBoughtEvent = labContract.GetEvent("AxieBought");
            var auctionCancelled = auctionContract.GetEvent("AuctionCancelled");
            //var chestPurchasedEvent = landPresaleContract.GetEvent("ChestPurchased");

            //set block range search
            var lastBlock = await GetLastBlockCheckpoint(web3);
            var firstBlock = new BlockParameter(new HexBigInteger(KeyGetter.GetLastCheckedBlock()));

            //prepare filters 
            var auctionFilterAll = auctionSuccesfulEvent.CreateFilterInput(firstBlock, lastBlock);
            var auctionCancelledFilterAll = auctionCancelled.CreateFilterInput(firstBlock, lastBlock);
            var auctionCreationFilterAll = auctionCreatedEvent.CreateFilterInput(firstBlock, lastBlock);
            //var labFilterAll = axieBoughtEvent.CreateFilterInput(firstBlock, lastBlock);
            //var landSaleFilterAll = chestPurchasedEvent.CreateFilterInput(firstBlock, lastBlock);

            //get logs from blockchain
            var auctionLogs = await auctionSuccesfulEvent.GetAllChanges<AuctionSuccessfulEvent>(auctionFilterAll);
            //var auctionCancelledLogs = await auctionSuccesfulEvent.GetAllChanges<AuctionCancelledEvent>(auctionFilterAll);
            //var labLogs = await axieBoughtEvent.GetAllChanges<AxieBoughtEvent>(labFilterAll);
            //var auctionCreationLogs = await auctionCreatedEvent.GetAllChanges<AuctionCreatedEvent>(auctionCreationFilterAll);
            //var landLogs = await chestPurchasedEvent.GetAllChanges<ChestPurchasedEvent>(landSaleFilterAll);

            var tagSaleTypeList = new Dictionary<AxieTag, SaleTypeList> {
                {AxieTag.Origin,       new SaleTypeList() },
                {AxieTag.MEO,          new SaleTypeList() },
                {AxieTag.MEO2,         new SaleTypeList() },
                {AxieTag.Agamogenesis, new SaleTypeList() },
                {AxieTag.Untagged,     new SaleTypeList() }
            };

            var mysticSaleTypeList = new Dictionary<int, SaleTypeList> {
                {-1, new SaleTypeList() }, //any mystic
                {1, new SaleTypeList() },
                {2, new SaleTypeList() },
                {3, new SaleTypeList() },
                {4, new SaleTypeList() }
            };
            var initialTime = await GetBlockTimeStamp(firstBlock.BlockNumber.Value, web3);

            foreach (var value in tagSaleTypeList.Values) value.AddNewDataPoint(LoopHandler.lastUnixTimeCheck);
            foreach (var value in mysticSaleTypeList.Values) value.AddNewDataPoint(LoopHandler.lastUnixTimeCheck);

            int eggCount = 0;
            //var landResult = new int[] { 0, 0, 0, 0 };
            //var landHolders = await DbFetch.FetchUniqueLandHolders();
            //var landGains = 0;
            //foreach (var log in labLogs)
            //{
            //    eggCount += log.Event.amount;
            //}

            var uniqueBuyers = await DbFetch.FetchUniqueBuyers();
            var uniqueGains = 0;
            foreach (var log in auctionLogs)
            {
                if (!uniqueBuyers.Contains(log.Event.winner))
                {
                    uniqueBuyers.Add(log.Event.winner);
                    await DatabaseConnection.GetDb().GetCollection<UniqueBuyer>("UniqueBuyers").InsertOneAsync(new UniqueBuyer(log.Event.winner));
                    uniqueGains++;
                }
                AxieDetailData axie = new AxieDetailData();
                try
                {
                    axie = await AxieDetailData.GetAxieFromApi(Convert.ToInt32(log.Event.tokenId.ToString()));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                if (axie.stage > 2)
                {
                    axie.GetBodyType();
                    if (axie.title != null)
                    {
                        if (axie.mysticCount > 0)
                        {
                            switch (axie.mysticCount)
                            {
                                case 1:
                                    mysticSaleTypeList[1].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                    break;
                                case 2:
                                    mysticSaleTypeList[2].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                    break;
                                case 3:
                                    mysticSaleTypeList[3].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                    break;
                                case 4:
                                    mysticSaleTypeList[4].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                    break;
                            }
                            mysticSaleTypeList[-1].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                        }
                        else
                        {
                            var tag = axie.title;
                            switch (tag)
                            {
                                case "Origin":
                                    tagSaleTypeList[AxieTag.Origin].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                    break;
                                case "MEO Corp":
                                    tagSaleTypeList[AxieTag.MEO].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                    break;
                                case "MEO Corp II":
                                    tagSaleTypeList[AxieTag.MEO2].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                    break;
                                case "Agamogenesis":
                                    tagSaleTypeList[AxieTag.Agamogenesis].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                    break;
                            }
                        }
                    }
                    else
                        tagSaleTypeList[AxieTag.Untagged].UpdateData(new DailySaleData(axie, log.Event.totalPrice));


                }
            }

            //foreach (var log in landLogs)
            //{
            //    landResult[log.Event.chestType]++;
            //    if (landHolders.Contains(log.Event.owner))
            //    {
            //        await DatabaseConnection.GetDb().GetCollection<UniqueBuyer>("UniqueLandHolders").InsertOneAsync(new UniqueBuyer(log.Event.owner));
            //        landGains++;
            //    }
            //}

            foreach (var t in tagSaleTypeList)
            {
                var tagCollec = DatabaseConnection.GetDb().GetCollection<DaySummary>(t.Key.ToString());
                foreach (var day in t.Value)
                {
                    await tagCollec.InsertOneAsync(day);
                }
            }
            foreach (var t in mysticSaleTypeList)
            {

                var collecName = t.Key.ToString();
                if (t.Key == -1) collecName = "AnyMystic";
                var mysticCollec = DatabaseConnection.GetDb().GetCollection<DaySummary>(collecName);
                foreach (var day in t.Value)
                {
                    await mysticCollec.InsertOneAsync(day);
                }
            }

            //await Utils.PushLandData(landResult, LoopHandler.lastUnixTimeCheck);

            await DatabaseConnection.GetDb().GetCollection<UniqueBuyerGain>("UniqueBuyerGains")
            .InsertOneAsync(new UniqueBuyerGain(LoopHandler.lastUnixTimeCheck, uniqueGains));

            //await DatabaseConnection.GetDb().GetCollection<UniqueBuyerGain>("UniqueLandholderGains")
            //.InsertOneAsync(new UniqueBuyerGain(LoopHandler.lastUnixTimeCheck, landGains));

            var collec = DatabaseConnection.GetDb().GetCollection<EggCount>("EggSoldPerDay");
            //await collec.InsertOneAsync(new EggCount(LoopHandler.lastUnixTimeCheck, eggCount));
            KeyGetter.SetLastCheckedBlock(lastBlock.BlockNumber.Value);
            Console.WriteLine("Pods sync done.");
        }

        public static async Task FetchAllSalesData()
        {
            var web3 = new Web3("https://mainnet.infura.io");
            //get contracts
            var auctionContract = web3.Eth.GetContract(KeyGetter.GetABI("auctionABI"), AxieCoreContractAddress);
            var coreContract = web3.Eth.GetContract(KeyGetter.GetABI("coreABI"), AxieCoreContractAddress);

            var getSellerInfoFunction = auctionContract.GetFunction("getAuction");
            var ownerOfFunction = coreContract.GetFunction("ownerOf");

            //get events
            var auctionSuccesfulEvent = auctionContract.GetEvent("AuctionSuccessful");

            //set block range search
            //var lastBlock = await GetLastBlockCheckpoint(web3);
            //var firstBlock = new BlockParameter(new HexBigInteger(KeyGetter.GetLastCheckedBlock()));
            object[] input1 = new object[2];
            input1[0] = "0xf5b0a3efb8e8e4c201e2a935f110eaaf3ffecb8d";
            input1[1] = new BigInteger(3021);
            object[] input2 = new object[1];
            input2[0] = new BigInteger(2247);
            var tada = await ownerOfFunction.CallDeserializingToObjectAsync<OwnerOf>(new BlockParameter(new HexBigInteger(new BigInteger(9351694))), input2);
            var sellerInfo1 = await getSellerInfoFunction.CallDeserializingToObjectAsync<SellerInfo>(
                                new BlockParameter(new HexBigInteger(new BigInteger(9351694))), input1);
            //await getSellerInfoFunction.CallAsync<SellerInfo>()
            Console.WriteLine("Hi!");
            return;
            //prepare filters 
            //var auctionFilterAll = auctionSuccesfulEvent.CreateFilterInput(firstBlock, lastBlock);
            //get logs from blockchain
            //var auctionLogs = await auctionSuccesfulEvent.GetAllChanges<AuctionSuccessfulEvent>(auctionFilterAll);
            BigInteger first = 9051317; //6727713
            BigInteger last = 9351317;
            BigInteger current = first;
            var initialTime = await GetBlockTimeStamp(first, web3);
            var auctionList = new List<AuctionSaleData>();
            while (current < last)
            {
                var latest = current + 25000;
                if (latest > last)
                    latest = last;
                var auctionFilterAll = auctionSuccesfulEvent.CreateFilterInput(new BlockParameter(new HexBigInteger(current)), new BlockParameter(new HexBigInteger(latest)));
                var auctionLogs = await auctionSuccesfulEvent.GetAllChanges<AuctionSuccessfulEvent>(auctionFilterAll);
                foreach (var log in auctionLogs)
                {
                    float price = Convert.ToSingle(Nethereum.Util.UnitConversion.Convert.FromWei(log.Event.totalPrice).ToString());
                    int token = Convert.ToInt32(log.Event.tokenId.ToString());
                    int time = await GetBlockTimeStamp(log.Log.BlockNumber.Value, web3);
                    object[] input = new object[2];
                    input[0] = NftAddress;
                    input[1] = log.Event.tokenId;
                    Console.WriteLine($"Token is {token}");
                    var sellerInfo = await getSellerInfoFunction.CallDeserializingToObjectAsync<SellerInfo>(
                                new BlockParameter(new HexBigInteger(log.Log.BlockNumber.Value)), input);
                    Console.ReadLine();
                    auctionList.Add(new AuctionSaleData(time, token, price, log.Event.winner, sellerInfo.seller));
                }
                DatabaseConnection.SetupConnection("AxieAuctionData");
                var auctionCollec = DatabaseConnection.GetDb().GetCollection<AuctionSaleData>("AuctionSales");
                await auctionCollec.InsertManyAsync(auctionList);
                auctionList.Clear();
                current = latest;
                break;
            }
            Console.WriteLine("Done!");

        }

        public static async Task FetchAllUniqueLandBuyers()
        {
            var web3 = new Web3("https://mainnet.infura.io");
            var lastBlock = await GetLastBlockCheckpoint(web3);
            var landContract = web3.Eth.GetContract(KeyGetter.GetABI("landSaleABI"), AxieLandPresaleContract);
            var chestPurchasedEvent = landContract.GetEvent("ChestPurchased");

            var uniqueBuyers = new List<string>();
            var lastBlockvalue = lastBlock.BlockNumber.Value;
            while (lastBlockChecked < lastBlockvalue)
            {
                var latest = lastBlockChecked + 10000;
                if (latest > lastBlockvalue)
                    latest = lastBlockvalue;
                var landFilterAll = chestPurchasedEvent.CreateFilterInput(new BlockParameter(new HexBigInteger(lastBlockChecked)), new BlockParameter(new HexBigInteger(latest)));
                var landLogs = await chestPurchasedEvent.GetAllChanges<ChestPurchasedEvent>(landFilterAll);


                foreach (var log in landLogs)
                {
                    //landResult[log.Event.chestType]++;
                    if (!uniqueBuyers.Contains(log.Event.buyer)) uniqueBuyers.Add(log.Event.buyer.ToLower());
                }
                lastBlockChecked += 10000;
            }
            var existing = await DbFetch.FetchUniqueLandHolders();
            var collec = DatabaseConnection.GetDb().GetCollection<UniqueBuyer>("UniqueLandHolders");
            foreach (var buyers in uniqueBuyers)
                if (!existing.Contains(buyers.ToLower()))
                    await collec.InsertOneAsync(new UniqueBuyer(buyers));
        }

        public static async Task FetchCumulUniqueLandBuyers()
        {
            var web3 = new Web3("https://mainnet.infura.io");
            var lastBlock = await GetLastBlockCheckpoint(web3);
            var auctionContract = web3.Eth.GetContract(KeyGetter.GetABI("landSaleABI"), AxieLandPresaleContract);
            var chestPurchasedEvent = auctionContract.GetEvent("ChestPurchased");

            var landResult = new int[] { 0, 0, 0, 0 };
            List<int> list = new List<int>();
            var collec = DatabaseConnection.GetDb().GetCollection<UniqueBuyerGain>("UniqueLandHoldersGains");
            var uniqueBuyers = new List<string>();
            var uniqueGains = 0;
            var initialTime = await GetBlockTimeStamp(lastBlockChecked, web3);
            var lastBlockvalue = lastBlock.BlockNumber.Value;
            while (lastBlockChecked < lastBlockvalue)
            {
                var latest = lastBlockChecked + 50000;
                if (latest > lastBlockvalue)
                    latest = lastBlockvalue;
                var auctionFilterAll = chestPurchasedEvent.CreateFilterInput(new BlockParameter(new HexBigInteger(lastBlockChecked)), new BlockParameter(new HexBigInteger(latest)));
                var auctionLogs = await chestPurchasedEvent.GetAllChanges<ChestPurchasedEvent>(auctionFilterAll);


                foreach (var log in auctionLogs)
                {
                    landResult[log.Event.chestType]++;
                    if (!uniqueBuyers.Contains(log.Event.buyer))
                    {
                        uniqueBuyers.Add(log.Event.buyer);
                        uniqueGains++;
                    }
                    var logTime = await GetBlockTimeStamp(log.Log.BlockNumber.Value, web3);
                    if (logTime - initialTime > 86400)
                    {
                        await Utils.PushLandData(landResult, LoopHandler.lastUnixTimeCheck);
                        landResult = new int[] { 0, 0, 0, 0 };
                        var diff = logTime - initialTime;
                        var mult = diff / 86400;
                        if (mult > 1)
                        {
                            for (int i = 1; i < mult + 1; i++)
                            {
                                if (i == mult)
                                    await collec.InsertOneAsync(new UniqueBuyerGain(initialTime, uniqueGains));
                                else await collec.InsertOneAsync(new UniqueBuyerGain(initialTime, 0));
                                if (i == mult)
                                    list.Add(uniqueGains);
                                else
                                    list.Add(0);
                                initialTime += 86400;
                            }
                        }
                        else
                        {
                            await collec.InsertOneAsync(new UniqueBuyerGain(initialTime, uniqueGains));
                            list.Add(uniqueGains);
                            initialTime += 86400;
                        }
                        uniqueGains = 0;
                    }
                }
                lastBlockChecked += 50000;
            }
            var sum = list.Sum();

            await collec.InsertOneAsync(new UniqueBuyerGain(initialTime + 86400, uniqueGains));
        }

        public static async Task FetchAllAuctionSales()
        {
            var tagSaleTypeList = new Dictionary<AxieTag, SaleTypeList> {
                {AxieTag.Origin,       new SaleTypeList() },
                {AxieTag.MEO,          new SaleTypeList() },
                {AxieTag.MEO2,         new SaleTypeList() },
                {AxieTag.Agamogenesis, new SaleTypeList() },
                {AxieTag.Untagged,     new SaleTypeList() }
            };

            var mysticSaleTypeList = new Dictionary<int, SaleTypeList> {
                {-1, new SaleTypeList() }, //any mystic
                {1, new SaleTypeList() },
                {2, new SaleTypeList() },
                {3, new SaleTypeList() },
                {4, new SaleTypeList() }
            };
            var web3 = new Web3("https://mainnet.infura.io");
            var lastBlock = await GetLastBlockCheckpoint(web3);
            var coreContract = web3.Eth.GetContract(KeyGetter.GetABI("auctionABI"), AxieCoreContractAddress);
            var auctionSuccessfulEvent = coreContract.GetEvent("AuctionSuccessful");

            var uniqueBuyers = new List<string>();
            //var lastBlockvalue = lastBlock.BlockNumber.Value;
            var lastBlockvalue = 7421144;
            var initialTime = await GetBlockTimeStamp(lastBlockChecked, web3);
            foreach (var value in tagSaleTypeList.Values) value.AddNewDataPoint(initialTime);
            foreach (var value in mysticSaleTypeList.Values) value.AddNewDataPoint(initialTime);

            while (lastBlockChecked < lastBlockvalue)
            {
                var latest = lastBlockChecked + 10000;
                if (latest > lastBlockvalue)
                    latest = lastBlockvalue;
                var auctionFilter = auctionSuccessfulEvent.CreateFilterInput(new BlockParameter(new HexBigInteger(lastBlockChecked)), new BlockParameter(new HexBigInteger(latest)));
                var auctionLogs = await auctionSuccessfulEvent.GetAllChanges<AuctionSuccessfulEvent>(auctionFilter);


                foreach (var log in auctionLogs)
                {
                    var logTime = await GetBlockTimeStamp(log.Log.BlockNumber.Value, web3);
                    var axie = await AxieDetailData.GetAxieFromApi(Convert.ToInt32(log.Event.tokenId.ToString()));
                    if (axie.stage > 2)
                    {
                        axie.GetBodyType();
                        if (axie.title != null)
                        {
                            if (axie.mysticCount > 0)
                            {
                                switch (axie.mysticCount)
                                {
                                    case 1:
                                        mysticSaleTypeList[1].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                        break;
                                    case 2:
                                        mysticSaleTypeList[2].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                        break;
                                    case 3:
                                        mysticSaleTypeList[3].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                        break;
                                    case 4:
                                        mysticSaleTypeList[4].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                        break;
                                }
                                mysticSaleTypeList[-1].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                            }
                            else
                            {
                                var tag = axie.title;
                                switch (tag)
                                {
                                    case "Origin":
                                        tagSaleTypeList[AxieTag.Origin].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                        break;
                                    case "MEO Corp":
                                        tagSaleTypeList[AxieTag.MEO].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                        break;
                                    case "MEO Corp II":
                                        tagSaleTypeList[AxieTag.MEO2].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                        break;
                                    case "Agamogenesis":
                                        tagSaleTypeList[AxieTag.Agamogenesis].UpdateData(new DailySaleData(axie, log.Event.totalPrice));
                                        break;
                                }
                            }
                        }
                        else
                            tagSaleTypeList[AxieTag.Untagged].UpdateData(new DailySaleData(axie, log.Event.totalPrice));


                    }
                    if (logTime - initialTime > 86400)
                    {
                        foreach (var value in tagSaleTypeList.Values) value.AddNewDataPoint(initialTime + 86400);
                        foreach (var value in mysticSaleTypeList.Values) value.AddNewDataPoint(initialTime + 86400);
                        initialTime += 86400;
                    }
                }
                lastBlockChecked += 10000;
            }

            foreach (var t in tagSaleTypeList)
            {
                var collec = DatabaseConnection.GetDb().GetCollection<DaySummary>(t.Key.ToString());
                foreach (var day in t.Value)
                {
                    await collec.InsertOneAsync(day);
                }
            }
            foreach (var t in mysticSaleTypeList)
            {

                var collecName = t.Key.ToString();
                if (t.Key == -1) collecName = "AnyMystic";
                var collec = DatabaseConnection.GetDb().GetCollection<DaySummary>(collecName);
                foreach (var day in t.Value)
                {
                    await collec.InsertOneAsync(day);
                }
            }
        }

        private static async Task<int> GetBlockTimeStamp(BigInteger number, Web3 web3)
        {
            var blockParam = new BlockParameter(new HexBigInteger(number));
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockParam);
            return Convert.ToInt32(block.Timestamp.Value.ToString());
        }

        private static async Task<BlockParameter> GetLastBlockCheckpoint(Web3 web3)
        {
            var lastBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var blockNumber = lastBlock.Value - 10;
            return new BlockParameter(new HexBigInteger(blockNumber));
        }

        private static BlockParameter GetInitialBlockCheckpoint(HexBigInteger blockNumber)
        {
            var firstBlock = blockNumber.Value - 10;
            return new BlockParameter(new HexBigInteger(firstBlock));
        }
    }


    [Event("AuctionSuccessful")]
    public class AuctionSuccessfulEvent : IEventDTO
    {
        [Parameter("address", "_nftAddress", 1, true)]
        public string nftAddress { get; set; }

        [Parameter("uint256", "_tokenId", 2, true)]
        public BigInteger tokenId { get; set; }

        [Parameter("uint256", "_totalPrice", 3)]
        public BigInteger totalPrice { get; set; }

        [Parameter("address", "_winner", 4)]
        public string winner { get; set; }
    }
    [Event("AxieBought")]
    public class AxieBoughtEvent : IEventDTO
    {
        [Parameter("address", "_buyer", 1, true)]
        public string buyer { get; set; }

        [Parameter("address", "_referrer", 2, true)]
        public string referrer { get; set; }

        [Parameter("int8", "_amount", 3)]
        public int amount { get; set; }

        [Parameter("uint256", "_price", 4)]
        public BigInteger price { get; set; }

        [Parameter("uint256", "_referralReward", 4)]
        public BigInteger referralReward { get; set; }
    }

    [Event("AuctionCreated")]
    public class AuctionCreatedEvent : IEventDTO
    {
        [Parameter("address", "_nftAddress", 1, true)]
        public string nftAddress { get; set; }

        [Parameter("uint256", "_tokenId", 2, true)]
        public BigInteger tokenId { get; set; }

        [Parameter("uint256", "_startingPrice", 3)]
        public BigInteger startingPrice { get; set; }

        [Parameter("uint256", "_endingPrice", 4)]
        public BigInteger endingPrice { get; set; }

        [Parameter("uint256", "_duration", 5)]
        public BigInteger duration { get; set; }

        [Parameter("address", "_seller", 6)]
        public string seller { get; set; }

    }
    [Event("AuctionCancelled")]
    public class AuctionCancelledEvent : IEventDTO
    {
        [Parameter("address", "_nftAddress", 1, true)]
        public string nftAddress { get; set; }

        [Parameter("uint256", "_tokenId", 2, true)]
        public BigInteger tokenId { get; set; }
    }
    [Event("ChestPurchased")]
    public class ChestPurchasedEvent : IEventDTO
    {
        [Parameter("uint8", "_chestType", 1, true)]
        public int chestType { get; set; }

        [Parameter("uint256", "_chestAmount", 2)]
        public BigInteger chestAmount { get; set; }

        [Parameter("address", "_tokenAddress", 3, true)]
        public string tokenAddress { get; set; }

        [Parameter("uint256", "_tokenAmount", 4)]
        public BigInteger tokenAmount { get; set; }

        [Parameter("uint256", "_totalPrice", 5)]
        public BigInteger totalPrice { get; set; }

        [Parameter("uint256", "_lunaCashbackAmount", 6)]
        public BigInteger lunaCashbackAmount { get; set; }

        [Parameter("address", "_buyer", 7)]
        public string buyer { get; set; }

        [Parameter("address", "_owner", 8, true)]
        public string owner { get; set; }
    }

    [FunctionOutput]
    public class AxieExtraData
    {
        [Parameter("uint256", "_sireId", 1)]
        public BigInteger sireId { get; set; }

        [Parameter("uint256", "_matronId", 2)]
        public BigInteger matronId { get; set; }

        [Parameter("uint256", "_exp", 3)]
        public BigInteger exp { get; set; }

        [Parameter("uint256", "_numBreeding", 4)]
        public BigInteger numBreeding { get; set; }
    }

    [FunctionOutput]
    public class SellerInfo
    {
        [Parameter("address", "seller", 1)]
        public string seller { get; set; }

        [Parameter("uint256", "startingPrice", 2)]
        public BigInteger startingPrice { get; set; }

        [Parameter("uint256", "endingPrice", 3)]
        public BigInteger endingPrice { get; set; }

        [Parameter("uint256", "duration", 4)]
        public BigInteger duration { get; set; }

        [Parameter("uint256", "startedAt", 5)]
        public BigInteger startedAt { get; set; }
    }

    [FunctionOutput]
    public class OwnerOf
    {
        [Parameter("address", "name", 1)]
        public string seller { get; set; }
    }

    [Function("getExtra")]
    public class AxieExtraFunction
    {
        [Parameter("uint256", "_axieId", 1)]
        public BigInteger axieId { get; set; }

    }

    [Function("bid")]
    public class BidFunction
    {
        [Parameter("address", "_nftAddress", 1)]
        public string address { get; set; }
        [Parameter("uint256", "_tokenId", 1)]
        public BigInteger tokenId { get; set; }
    }
}
