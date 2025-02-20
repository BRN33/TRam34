using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Persistence.Data
{
    public class MongoDeneme
    {
        public readonly IConfiguration _configuration;
        public readonly IMongoDatabase? _database;

        public MongoDeneme(IConfiguration configuration)
        {
            _configuration = configuration;

            var connectionString = _configuration.GetConnectionString("DbConnection");
            var mongoUrl = MongoUrl.Create(connectionString);
            var mongoClient = new MongoClient(mongoUrl);
            _database = mongoClient.GetDatabase(mongoUrl.DatabaseName);
            Console.WriteLine("DB baglantısı kuruldu");

        }


        public IMongoDatabase? Database => _database;

    }
}
