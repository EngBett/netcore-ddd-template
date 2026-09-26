using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Template.Infrastructure.Extensions
{
    public static class RDFacadeExtensions
    {
        public static async Task<IEnumerable<T>> GetModelFromQuery<T>(this DatabaseFacade databaseFacade, string sql, params object[] parameters)
            where T : new()
        {
            var reader = await databaseFacade.ExecuteSqlQuery(sql, parameters);
            using (DbDataReader dr = reader.DbDataReader)
            {
                List<T> lst = new List<T>();
                PropertyInfo[] props = typeof(T).GetProperties();
                while (dr.Read())
                {
                    T t = new T();
                    IEnumerable<string> actualNames = dr.GetColumnSchema().Select(o => o.ColumnName);
                    for (int i = 0; i < props.Length; ++i)
                    {
                        PropertyInfo pi = props[i];

                        if (!pi.CanWrite)
                        {
                            continue;
                        }

                        var ca = pi.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>();
                        string name = ca?.Name ?? pi.Name;

                        if (!actualNames.Contains(name))
                        {
                            continue;
                        }

                        object? value = dr[name];

                        // The property's own type, not the type that declares it.
                        // DeclaringType is the entity class, so every column was
                        // tested for nullability against the wrong type and the
                        // Nullable<> check below could never be true.
                        Type pt = pi.PropertyType;
                        bool nullable = pt.GetTypeInfo().IsGenericType && pt.GetGenericTypeDefinition() == typeof(Nullable<>);
                        if (value == DBNull.Value)
                        {
                            value = null;
                        }
                        if (value is null && pt.GetTypeInfo().IsValueType && !nullable)
                        {
                            value = Activator.CreateInstance(pt);
                        }
                        pi.SetValue(t, value);
                    }//for i
                    lst.Add(t);
                }//while
                return lst;
            }//using dr
        }

        public static async Task<RelationalDataReader> ExecuteSqlQuery(this DatabaseFacade databaseFacade, string sql, params object[] parameters)
        {
            var concurrencyDetector = databaseFacade.GetService<IConcurrencyDetector>();

            using (concurrencyDetector.EnterCriticalSection())
            {
                var rawSqlCommand = databaseFacade
                    .GetService<IRawSqlCommandBuilder>()
                    .Build(sql, parameters);

                var paramObject = new RelationalCommandParameterObject(databaseFacade.GetService<IRelationalConnection>(), rawSqlCommand.ParameterValues, null, null, null);

                return await rawSqlCommand
                    .RelationalCommand
                    .ExecuteReaderAsync(paramObject);
            }
        }
    }
}