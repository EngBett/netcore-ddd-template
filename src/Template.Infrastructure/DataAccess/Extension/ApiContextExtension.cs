using Template.Domain.Models;
using Template.Infrastructure.DataAccess;

namespace Template.Infrastructure.DataAccess.Extension
{
    public static class ApiContextExtension
    {
        public static async Task<int> NextValueForSequence(this ApplicationContext pCtx, DatabaseSequence pSequence)
        {
            return await pCtx.GetNextSequence(pSequence);
        }
    }
}
