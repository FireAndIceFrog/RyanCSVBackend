﻿using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDB.Models;

namespace MongoDB
{
    public interface IMongoDBConnector
    {
        Task InsertOneAsync(string collectionName, BsonDocument document);
        Task<IMongoQueryable<BsonDocument>> GetDocumentsAsync(string collectionName);
        Task DeleteDocAsync(string collectionName, FilterDefinition<BsonDocument> document);
        Task<UpdateResult> UpdateOneAsync(string collectionName, FilterDefinition<BsonDocument> oldDoc, BsonDocument document);
        Task<ReplaceOneResult> ReplaceOneAsync(string collectionName, FilterDefinition<BsonDocument> oldDoc, BsonDocument document);
        Task InsertManyAsync(string collectionName, IEnumerable<BsonDocument> documents);
        Task DeleteAllAsync(string collectionName);
        Task<IAsyncCursor<BsonDocument>> FindNearAsync(string collectionName, double x, double y);
    }
}