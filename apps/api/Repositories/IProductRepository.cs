using AuraPrintsApi.Models;

namespace AuraPrintsApi.Repositories;

public interface IProductRepository
{
    ProductData GetAll();
}