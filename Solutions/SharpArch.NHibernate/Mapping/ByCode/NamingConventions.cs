using System;
//Needs a ref. to System.Data.Entity.Design.dll
using System.Data.Entity.Design.PluralizationServices;
using System.Globalization;
using NHibernate.Mapping.ByCode;
using System.Reflection;

namespace SharpArch.NHibernate.Mapping.ByCode
{
    public static class NamingConventions
    {
        #region Fields (1)

        private static readonly PluralizationService Service = PluralizationService.CreateService(CultureInfo.GetCultureInfo("en"));

        #endregion Fields

        #region Methods (21)

        // Public Methods (21) 

        public static void ApplyNamingConventions(this ModelMapper mapper)
        {
            mapper.BeforeMapClass += (modelInspector, type, map) =>
            {
                PluralizeEntityName(type, map);
                PrimaryKeyConvention(type, map);
            };
            mapper.BeforeMapManyToOne += ReferenceConvention;
            mapper.BeforeMapBag += OneToManyConvention;
            mapper.BeforeMapManyToMany += ManyToManyConvention;
            mapper.BeforeMapJoinedSubclass += MapJoinedSubclass;
            mapper.BeforeMapAny += MapAny;
            mapper.BeforeMapComponent += MapComponent;
            mapper.BeforeMapElement += MapElement;
            mapper.BeforeMapIdBag += MapIdBag;
            mapper.BeforeMapList += MapList;
            mapper.BeforeMapMap += MapMap;
            mapper.BeforeMapMapKey += MapMapKey;
            mapper.BeforeMapMapKeyManyToMany += MapMapKeyManyToMany;
            mapper.BeforeMapOneToMany += MapOneToMany;
            mapper.BeforeMapOneToOne += MapOneToOne;
            mapper.BeforeMapProperty += MapProperty;
            mapper.BeforeMapSet += MapSet;
            mapper.BeforeMapSubclass += MapSubclass;
            mapper.BeforeMapUnionSubclass += MapUnionSubclass;
        }

        public static void ManyToManyConvention(IModelInspector modelInspector, PropertyPath member, IManyToManyMapper map)
        {
            map.ForeignKey(
                string.Format("FK_{0}_{1}",
                       member.LocalMember.Name,
                       member.GetContainerEntity(modelInspector).Name));
        }

        public static void MapAny(IModelInspector modelInspector, PropertyPath member, IAnyMapper map)
        {
        }

        public static void MapComponent(IModelInspector modelInspector, PropertyPath member, IComponentAttributesMapper map)
        {
        }

        public static void MapElement(IModelInspector modelInspector, PropertyPath member, IElementMapper map)
        {
        }

        public static void MapIdBag(IModelInspector modelInspector, PropertyPath member, IIdBagPropertiesMapper map)
        {
        }

        public static void MapJoinedSubclass(IModelInspector modelInspector, Type type, IJoinedSubclassAttributesMapper map)
        {
            map.Table(Service.Pluralize(type.Name));
            map.Key(x =>
            {
                x.ForeignKey(string.Format("FK_{0}_{1}",
                                                type.BaseType.Name,
                                                type.Name));
                x.Column(type.Name + "Id");
            });
        }

        public static void MapList(IModelInspector modelInspector, PropertyPath member, IListPropertiesMapper map)
        {
        }

        public static void MapMap(IModelInspector modelInspector, PropertyPath member, IMapPropertiesMapper map)
        {
        }

        public static void MapMapKey(IModelInspector modelInspector, PropertyPath member, IMapKeyMapper map)
        {
        }

        public static void MapMapKeyManyToMany(IModelInspector modelInspector, PropertyPath member, IMapKeyManyToManyMapper map)
        {
        }

        public static void MapOneToMany(IModelInspector modelInspector, PropertyPath member, IOneToManyMapper map)
        {
        }

        public static void MapOneToOne(IModelInspector modelInspector, PropertyPath member, IOneToOneMapper map)
        {
        }

        public static void MapProperty(IModelInspector modelInspector, PropertyPath member, IPropertyMapper map)
        {
            ComponentNamingConvention(modelInspector, member, map);
        }

        public static void ComponentNamingConvention(IModelInspector modelInspector, PropertyPath member, IPropertyMapper map)
        {
            var property = member.LocalMember as PropertyInfo;
            if (modelInspector.IsComponent(property.DeclaringType))
            {
                map.Column(member.PreviousPath.LocalMember.Name + member.LocalMember.Name);
            }
        }

        public static void MapSet(IModelInspector modelInspector, PropertyPath member, ISetPropertiesMapper map)
        {
        }

        public static void MapSubclass(IModelInspector modelInspector, Type type, ISubclassAttributesMapper map)
        {
        }

        public static void MapUnionSubclass(IModelInspector modelInspector, Type type, IUnionSubclassAttributesMapper map)
        {
        }

        public static void OneToManyConvention(IModelInspector modelInspector, PropertyPath member, IBagPropertiesMapper map)
        {
            var inv = member.LocalMember.GetInverseProperty();
            if (inv == null)
            {
                map.Key(x => x.Column(member.GetContainerEntity(modelInspector).Name + "Id"));
                map.Cascade(Cascade.All | Cascade.DeleteOrphans);
                map.BatchSize(20);
                map.Inverse(true);
            }
        }

        public static void PluralizeEntityName(Type type, IClassAttributesMapper map)
        {
            map.Table(Service.Pluralize(type.Name));
        }

        public static void PrimaryKeyConvention(Type type, IClassAttributesMapper map)
        {
            map.Id(k =>
            {
                k.Generator(Generators.Native);
                k.Column("Id");
            });
        }

        public static void ReferenceConvention(IModelInspector modelInspector, PropertyPath member, IManyToOneMapper map)
        {
            map.Column(k => k.Name(member.LocalMember.GetPropertyOrFieldType().Name + "Id"));
            map.ForeignKey(
                string.Format("FK_{0}_{1}",
                       member.LocalMember.Name,
                       member.GetContainerEntity(modelInspector).Name));
            map.Cascade(Cascade.All | Cascade.DeleteOrphans);
        }

        #endregion Methods
    }
}
