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
            var auctionCollec = DatabaseConnection.GetDb().GetCollection<AuctionSaleData>("AuctionSales");
            var auctionCreateCollec = DatabaseConnection.GetDb().GetCollection<AuctionCreationData>("AuctionCreations");
            var auctionList = await (await auctionCollec.FindAsync(x => true)).ToListAsync();
            var auctionCreateList = await(await auctionCreateCollec.FindAsync(x => true)).ToListAsync();
            Console.WriteLine("Hi!");
            var addList = new List<Address>();
            foreach (var sale in auctionList)
            {
                if (addList.FirstOrDefault(x => x.id == sale.buyer.ToLower()) == null)
                    addList.Add(new Address(sale.buyer.ToLower()));
                var sellerId = auctionCreateList.Where(a => a.block < sale.block && a.tokenId == sale.tokenId)
                                                .OrderBy(a => a.block)
                                                .Last().seller.ToLower();
                if (addList.FirstOrDefault(x => x.id == sellerId) == null)
                    addList.Add(new Address(sellerId));
                var buyer = addList.FirstOrDefault(x => x.id == sale.buyer.ToLower());
                var seller = addList.FirstOrDefault(x => x.id == sellerId);
                buyer.boughtCount++;
                buyer.bought += sale.price;
                seller.soldCount++;
                seller.sold += sale.price;
            }
            var addressCollec = DatabaseConnection.GetDb().GetCollection<Address>("SaleRegistry");
            await addressCollec.InsertManyAsync(addList);
        }
    }

    public class AuctionSaleData
    {
        public ObjectId _id;
        public int tokenId;
        public float price;
        public string buyer;
        public ulong block;

        public AuctionSaleData(int token, float _price, string _b, ulong _bl)
        {
            tokenId = token;
            price = _price;
            buyer = _b;
            block = _bl;
        }
    }

    public class AuctionCreationData
    {
        public ObjectId _id;
        public int tokenId;
        public string seller;
        public ulong block;

        public AuctionCreationData(int token, string _s, ulong _bl)
        {
            tokenId = token;
            seller = _s;
            block = _bl;
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
