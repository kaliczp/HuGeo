using HuGeo.DataAccess.Models;

namespace HuGeo.DataAccess.Loaders;

public interface IGridDataLoader
{
    Task<List<Hd72GridPoint>> LoadHd72GridAsync();
    Task<List<Wgs84GridPoint>> LoadWgs84GridAsync();
    string GetLoadSource();
}
