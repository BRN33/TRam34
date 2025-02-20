using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Persistence.Models
{
    public class TrainConfiguration
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string? TrainId { get; set; }
        public List<Hardware>? Hardware { get; set; }
        public List<Software>? Software { get; set; }
    }
}
