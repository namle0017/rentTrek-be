using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class PostTagRepository : GenericRepository<PostTag>, IPostTagRepository
{
    public PostTagRepository(AppDbContext context) : base(context)
    {
    }
}
