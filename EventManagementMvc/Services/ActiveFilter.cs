using System.Collections.Generic;
using System.Linq;

namespace EventManagementMvc.Services;

public static class ActiveFilter
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, bool isAdmin, System.Func<T, bool> isActivePredicate)
    {
        if (isAdmin) return query;
        return query.Where(isActivePredicate).AsQueryable();
    }
}
