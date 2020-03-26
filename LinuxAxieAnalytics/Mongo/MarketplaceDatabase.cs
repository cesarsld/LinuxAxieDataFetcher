using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.Linq;
using AxieDataFetcher.MarketPlaceDatabase;
using AxieDataFetcher.AxieObjects;

namespace AxieDataFetcher.Mongo
{
    public class MarketplaceDatabase
    {
        public static async Task AddNewAxie(int id)
        {
            var collec = DatabaseConnection.GetDb().GetCollection<AxieObjectV2>("MarketplaceDatabase");
            var axie = (await collec.FindAsync(a => a.id == id)).FirstOrDefault();
            if (axie == null)
                await collec.InsertOneAsync(await AxieObjectV2.GetAxieFromApi(id));
        }

        public static async Task RemoveAxie(int id)
        {
            var collec = DatabaseConnection.GetDb().GetCollection<AxieObjectV2>("MarketplaceDatabase");
            var axie = (await collec.FindAsync(a => a.id == id)).FirstOrDefault();
            if (axie != null)
                await collec.DeleteOneAsync(a => a.id == id);
        }

        public static async Task SetupInitialData()
        {
            var list = await ApiCalls.GetAxieListFromMarketplace();
            var collec = DatabaseConnection.GetDb().GetCollection<AxieObjectV2>("MarketplaceDatabase");
            await collec.InsertManyAsync(list);
        }



        public static async Task ComputeAllSales()
        {
            DatabaseConnection.SetupConnection("AxieAuctionData");
            var checkCollec = DatabaseConnection.GetDb().GetCollection<Checkpoint>("Checkpoints");
            var checkpoint = (await checkCollec.FindAsync(c => c.id == 1)).FirstOrDefault();
            var auctionCollec = DatabaseConnection.GetDb().GetCollection<AuctionSaleData>("AuctionSales");
            var auctionCreateCollec = DatabaseConnection.GetDb().GetCollection<AuctionCreationData>("AuctionCreations");
            var auctionList = await (await auctionCollec.FindAsync(x => x.block > (ulong)checkpoint.lastBlockReviewed)).ToListAsync();
            var auctionCreateList = await(await auctionCreateCollec.FindAsync(x => true)).ToListAsync();
            var addressCollec = DatabaseConnection.GetDb().GetCollection<Address>("SaleRegistry");
            var addList = await (await addressCollec.FindAsync(a => true)).ToListAsync();
            Console.WriteLine($"list count is {addList.Count}");
            var updateList = new List<string>();
            foreach (var sale in auctionList)
            {
                if (!updateList.Contains(sale.buyer))
                    updateList.Add(sale.buyer);
                if (addList.FirstOrDefault(x => x.id == sale.buyer.ToLower()) == null)
                    addList.Add(new Address(sale.buyer.ToLower()));
                var sellerId = auctionCreateList.Where(a => a.block < sale.block && a.tokenId == sale.tokenId)
                                                .OrderBy(a => a.block)
                                                .Last().seller.ToLower();
                if (!updateList.Contains(sellerId))
                    updateList.Add(sellerId);
                if (addList.FirstOrDefault(x => x.id == sellerId) == null)
                    addList.Add(new Address(sellerId));
                var buyer = addList.FirstOrDefault(x => x.id == sale.buyer.ToLower());
                var seller = addList.FirstOrDefault(x => x.id == sellerId);
                buyer.boughtCount++;
                buyer.bought += sale.price;
                seller.soldCount++;
                seller.sold += sale.price;
            }
            foreach (var add in updateList)
                await addressCollec.FindOneAndReplaceAsync(a => a.id == add, addList.FirstOrDefault(a => a.id == add));
            checkpoint.lastBlockReviewed = checkpoint.lastBlockChecked;
            await checkCollec.FindOneAndReplaceAsync(c => c.id == 1, checkpoint);
        }
    }

    public class AuctionSaleData
    {
        public long id;
        public int tokenId;
        public float price;
        public string buyer;
        public ulong block;

        public AuctionSaleData(long _id, int token, float _price, string _b, ulong _bl)
        {
            id = _id;
            tokenId = token;
            price = _price;
            buyer = _b;
            block = _bl;
        }
    }

    public class OpenseaSaleData
    {
        public int tokenId;
        public float price;
        public string buyer;
        public string seller;
        public ulong block;

        public OpenseaSaleData(int token, float _price, string _b, string _s, ulong _bl)
        {
            tokenId = token;
            price = _price;
            buyer = _b;
            seller = _s;
            block = _bl;
        }
    }

    public class AuctionCreationData
    {
        public long id;
        public int tokenId;
        public string seller;
        public ulong block;

        public AuctionCreationData(long _id, int token, string _s, ulong _bl)
        {
            id = _id;
            tokenId = token;
            seller = _s;
            block = _bl;
        }
    }

    public class Checkpoint
    {
        public int id;
        public int lastBlockChecked;
        public int lastBlockReviewed;
        public long totalCreations;
        public long totalSuccess;
        public Checkpoint(int _id, int _lastBlockChecked, int _lastBlockReviewed, long _totalCreations, long _totalSuccess)
        {
            id = _id;
            lastBlockChecked = _lastBlockChecked;
            lastBlockReviewed = _lastBlockReviewed;
            totalCreations = _totalCreations;
            totalSuccess = _totalSuccess;
        }
    }

    public class Address
    {
        public string id;
        public float bought;
        public float sold;
        public int boughtCount;
        public int soldCount;

        public Address(string _a)
        {
            id = _a;
            bought = 0f;
            sold = 0f;
            boughtCount = 0;
            soldCount = 0;
        }
    }
}
