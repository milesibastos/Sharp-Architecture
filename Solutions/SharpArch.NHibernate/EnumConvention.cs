using System;
using System.Linq;
using System.Reflection;
using NHibernate.Mapping.ByCode;
using NHibernate.Type;

namespace SharpArch.NHibernate
{
    public static class EnumConvention
    {
        #region Methods (4)

        // Public Methods (2) 

        public static bool IsEnum(this PropertyInfo property)
        {
            return property.PropertyType.IsEnum || (property.PropertyType.IsGenericType &&
                         property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                         property.PropertyType.GetGenericArguments()[0].IsEnum);
        }

        public static void MapAllEnumsToStrings(this ModelMapper mapper)
        {
            mapper.BeforeMapProperty += mapProperty;
        }
        // Private Methods (2) 

        private static void callGenericTypeMethod(IPropertyMapper map, PropertyInfo property)
        {
            var enumStringOfPropertyType = typeof(EnumStringType<>).MakeGenericType(property.PropertyType);
            var method = map.GetType().GetMethods().First(x => x.Name == "Type" && !x.GetParameters().Any());
            var genericMethod = method.MakeGenericMethod(new[] { enumStringOfPropertyType });
            genericMethod.Invoke(map, null);
        }

        private static void mapProperty(IModelInspector modelInspector, PropertyPath member, IPropertyMapper map)
        {
            var property = member.LocalMember as PropertyInfo;
            if (property == null) return;
            if (IsEnum(property))
            {
                callGenericTypeMethod(map, property);
            }
        }

        #endregion Methods
    }
}
