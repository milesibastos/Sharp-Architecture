namespace SharpArch.NHibernate.FluentNHibernate
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using global::FluentNHibernate.Automapping;
    using global::FluentNHibernate.Cfg;
    using global::FluentNHibernate.Cfg.Db;

    using global::NHibernate;
    using global::NHibernate.Cfg;
    using global::NHibernate.Event;

    using SharpArch.NHibernate.NHibernateValidator;

    public static class NHibernateFactory
    {
        public static Configuration Init(ISessionStorage storage, string[] mappingAssemblies)
        {
            return Init(storage, mappingAssemblies, null, null, null, null, null);
        }

        public static Configuration Init(ISessionStorage storage, string[] mappingAssemblies, string cfgFile)
        {
            return Init(storage, mappingAssemblies, null, cfgFile, null, null, null);
        }

        public static Configuration Init(
            ISessionStorage storage, string[] mappingAssemblies, IDictionary<string, string> cfgProperties)
        {
            return Init(storage, mappingAssemblies, null, null, cfgProperties, null, null);
        }

        public static Configuration Init(
            ISessionStorage storage, string[] mappingAssemblies, string cfgFile, string validatorCfgFile)
        {
            return Init(storage, mappingAssemblies, null, cfgFile, null, validatorCfgFile, null);
        }

        public static Configuration Init(
            ISessionStorage storage, string[] mappingAssemblies, AutoPersistenceModel autoPersistenceModel)
        {
            return Init(storage, mappingAssemblies, autoPersistenceModel, null, null, null, null);
        }

        public static Configuration Init(
            ISessionStorage storage,
            string[] mappingAssemblies,
            AutoPersistenceModel autoPersistenceModel,
            string cfgFile)
        {
            return Init(storage, mappingAssemblies, autoPersistenceModel, cfgFile, null, null, null);
        }

        public static Configuration Init(
            ISessionStorage storage,
            string[] mappingAssemblies,
            AutoPersistenceModel autoPersistenceModel,
            IDictionary<string, string> cfgProperties)
        {
            return Init(storage, mappingAssemblies, autoPersistenceModel, null, cfgProperties, null, null);
        }

        public static Configuration Init(
            ISessionStorage storage,
            string[] mappingAssemblies,
            AutoPersistenceModel autoPersistenceModel,
            string cfgFile,
            string validatorCfgFile)
        {
            return Init(storage, mappingAssemblies, autoPersistenceModel, cfgFile, null, validatorCfgFile, null);
        }

        public static Configuration Init(
            ISessionStorage storage,
            string[] mappingAssemblies,
            AutoPersistenceModel autoPersistenceModel,
            string cfgFile,
            IDictionary<string, string> cfgProperties,
            string validatorCfgFile)
        {
            return Init(
                storage, mappingAssemblies, autoPersistenceModel, cfgFile, cfgProperties, validatorCfgFile, null);
        }

        public static Configuration Init(
            ISessionStorage storage,
            string[] mappingAssemblies,
            AutoPersistenceModel autoPersistenceModel,
            string cfgFile,
            IDictionary<string, string> cfgProperties,
            string validatorCfgFile,
            IPersistenceConfigurer persistenceConfigurer)
        {
            NHibernateSession.InitStorage(storage);
            try
            {
                return AddConfiguration(
                    NHibernateSession.DefaultFactoryKey,
                    mappingAssemblies,
                    autoPersistenceModel,
                    cfgFile,
                    cfgProperties,
                    validatorCfgFile,
                    persistenceConfigurer);
            }
            catch
            {
                // If this NHibernate config throws an exception, null the Storage reference so 
                // the config can be corrected without having to restart the web application.
                NHibernateSession.Storage = null;
                throw;
            }
        }

        public static Configuration AddConfiguration(
            string factoryKey,
            string[] mappingAssemblies,
            AutoPersistenceModel autoPersistenceModel,
            string cfgFile,
            IDictionary<string, string> cfgProperties,
            string validatorCfgFile,
            IPersistenceConfigurer persistenceConfigurer)
        {
            Configuration config;
            var configCache = NHibernateSession.ConfigurationCache;
            if (configCache != null)
            {
                config = configCache.LoadConfiguration(factoryKey, cfgFile, mappingAssemblies);
                if (config != null)
                {
                    return NHibernateSession.AddConfiguration(factoryKey, config.BuildSessionFactory(), config, validatorCfgFile);
                }
            }

            config = AddConfiguration(
                factoryKey,
                mappingAssemblies,
                autoPersistenceModel,
                NHibernateSession.ConfigureNHibernate(cfgFile, cfgProperties),
                validatorCfgFile,
                persistenceConfigurer);

            if (configCache != null)
            {
                configCache.SaveConfiguration(factoryKey, config);
            }

            return config;
        }

        public static Configuration AddConfiguration(
            string factoryKey,
            string[] mappingAssemblies,
            AutoPersistenceModel autoPersistenceModel,
            Configuration cfg,
            string validatorCfgFile,
            IPersistenceConfigurer persistenceConfigurer)
        {
            var sessionFactory = CreateSessionFactoryFor(
                mappingAssemblies, autoPersistenceModel, cfg, persistenceConfigurer);

            return NHibernateSession.AddConfiguration(factoryKey, sessionFactory, cfg, validatorCfgFile);
        }

        private static ISessionFactory CreateSessionFactoryFor(
            IEnumerable<string> mappingAssemblies,
            AutoPersistenceModel autoPersistenceModel,
            Configuration cfg,
            IPersistenceConfigurer persistenceConfigurer)
        {
            var fluentConfiguration = Fluently.Configure(cfg);

            if (persistenceConfigurer != null)
            {
                fluentConfiguration.Database(persistenceConfigurer);
            }

            fluentConfiguration.Mappings(
                m =>
                {
                    foreach (var mappingAssembly in mappingAssemblies)
                    {
                        var assembly = Assembly.LoadFrom(MakeLoadReadyAssemblyName(mappingAssembly));

                        m.HbmMappings.AddFromAssembly(assembly);
                        m.FluentMappings.AddFromAssembly(assembly).Conventions.AddAssembly(assembly);
                    }

                    if (autoPersistenceModel != null)
                    {
                        m.AutoMappings.Add(autoPersistenceModel);
                    }
                });

            fluentConfiguration.ExposeConfiguration(
                e =>
                {
                    e.EventListeners.PreInsertEventListeners = new IPreInsertEventListener[]
                            {
                                new DataAnnotationsEventListener()
                            };
                    e.EventListeners.PreUpdateEventListeners = new IPreUpdateEventListener[]
                            {
                                new DataAnnotationsEventListener()
                            };
                });

            return fluentConfiguration.BuildSessionFactory();
        }

        private static string MakeLoadReadyAssemblyName(string assemblyName)
        {
            return (assemblyName.IndexOf(".dll") == -1) ? assemblyName.Trim() + ".dll" : assemblyName.Trim();
        }
    }
}