﻿using MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Newtonsoft.Json;
using CSVBackend.Map.models;

namespace CSVBackend.Map.Services;

public class MapLayerService : IMapLayerService
{
    private readonly IMongoDBConnector _mongoDataAccess;
    public MapLayerService(IMongoDBConnector mongoDataAccess)
    {
        _mongoDataAccess = mongoDataAccess;

    }
    private string _collectionName { get => $"map_layers"; }
    private string _tableHeaderName { get => $"{_collectionName}_headers"; }
    private int _currentWeek { get => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(DateTime.Now, CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday); }
    private readonly string timestampField = "timestamp";
    private readonly string headerIdField = "HeaderId";
    private Guid _id = Guid.Empty;

    private BsonDocument CreateDocument<T>(GeoJsonModel<T> model)
    {
        return new BsonDocument()
        {
            new BsonElement("type", new BsonString(model?.type)),
            new BsonElement("geometry", new BsonDocument()
            {
                new BsonElement("type", new BsonString(model?.geometry?.type)),
                new BsonElement("coordinates", new BsonArray(model?.geometry?.coordinates)),
            }),
            new BsonElement("properties", new BsonDocument()
            {
                new BsonElement("backgroundColor", new BsonString(model?.properties?.backgroundColor)),
                new BsonElement("borderColor", new BsonString(model?.properties?.borderColor)),
            })
        };
    }

    public async Task<string> GetFeatures(double x, double y, double z)
    {
        var BsonId = new BsonBinaryData(_id, GuidRepresentation.Standard);
        var asyncDocs = await _mongoDataAccess.FindNearAsync(_collectionName, x, y);

        var docs = await asyncDocs.ToListAsync();
        var processedDocs = docs
            .Select((doc) =>
            {
                var id = doc.GetValue("_id").AsObjectId;
                doc.Remove("_id");

                var properties = doc.GetValue("properties");
                properties.AsBsonDocument.Set("id", id.ToString());
                doc.Set("properties", properties);
                return doc;
            });

        return new  { features = processedDocs }.ToJson();
    }

    public async Task UpdateFeatures(object data)
    {
        if(data == null)
        {
            return;
        }
        var deserializedData = JsonConvert.DeserializeObject<GeoJsonModelList<dynamic>>(data.ToString());

        var tasks = deserializedData?.features?.Select(async (item) =>
        {
            if (item?.geometry?.coordinates != null)
            {
                item.geometry.coordinates = item.geometry.coordinates.Select((ring) =>
                {
                    return ring.Select((point) =>
                    {
                        if (point is JArray)
                            return (point as JArray).Select((coordinate) => double.Parse(coordinate.ToString()));
                        else
                            return double.Parse(point);
                    })
                    .ToList();
                })
                .ToList();
            }

            var update = CreateDocument(item!);
            update.Add(new BsonElement("_id", new ObjectId(item?.properties?.id ?? "null")));

            var builder = Builders<BsonDocument>.Filter;
            return await _mongoDataAccess.ReplaceOneAsync(_collectionName, builder.Eq("_id", new ObjectId(item?.properties?.id)), update);
        })
        .ToArray();

        if(tasks != null && tasks.Any())
        {
            var results = await Task.WhenAll(tasks);
        }
        return;
    }

    public async Task CreateFeature(object data)
    {
        if (data == null)
        {
            return;
        }
        var deserializedData = JsonConvert.DeserializeObject<GeoJsonModelList<dynamic>>(data.ToString());

        var tasks = deserializedData?.features?.Select(async (item) =>
        {
            if (item?.geometry?.coordinates != null)
            {
                item.geometry.coordinates = item.geometry.coordinates.Select((ring) =>
                {
                    return ring.Select((point) =>
                    {
                        if (point is JArray)
                            return (point as JArray).Select((coordinate) => double.Parse(coordinate.ToString()));
                        else
                            return double.Parse(point);
                    })
                    .ToList();
                })
                .ToList();
            }


            var insert = CreateDocument(item!);

            return _mongoDataAccess.InsertOneAsync(_collectionName, insert);
        })
        .ToArray();

        if (tasks != null && tasks.Any())
        {
            var results = await Task.WhenAll(tasks);
        }
        return;
    }

    public async Task<bool> ClearAllData()
    {

        await _mongoDataAccess.DeleteManyAsync(_collectionName, new BsonDocument());
        await _mongoDataAccess.DeleteManyAsync(_tableHeaderName, new BsonDocument());

        return true;
    }

    public async Task DeleteFeaturesAsync(object data)
    {
        if(data == null)
        {
            return;
        }

        var deserializedData = JsonConvert.DeserializeObject<List<string>>(data.ToString()?? "[]");
        
        var builder = Builders<BsonDocument>.Filter;

        var containsIdList = deserializedData!.Select((id) => builder.Eq("_id", new ObjectId(id)));
        var filter = builder.Or(
            containsIdList
        );
        await _mongoDataAccess.DeleteManyAsync(_collectionName, filter);
    }
}
