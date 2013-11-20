namespace SharpArch.NHibernate
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using Domain;

    using global::FluentNHibernate.Automapping;
    using global::FluentNHibernate.Cfg;
    using global::FluentNHibernate.Cfg.Db;

    using global::NHibernate;
    using global::NHibernate.Connection;
    using global::NHibernate.Dialect;
    using global::NHibernate.Driver;
    using global::NHibernate.Cfg;
    using global::NHibernate.Event;
    using global::NHibernate.Cfg.MappingSchema;
    using global::NHibernate.Mapping.ByCode;
    using global::NHibernate.Validator.Engine;
    using global::NHibernate.Validator.Cfg.Loquacious;
    using global::NHibernate.Validator.Cfg;
    // using global::NHibernate.Validator.Engine;

    using SharpArch.NHibernate.NHibernateValidator;
    using SharpArch.NHibernate.Mapping.ByCode;
    using System.Data;

    public static class NHibernateSession
    {
        /// <summary>
        ///     The default factory key used if only one database is being communicated with.
        /// </summary>
        public static readonly string DefaultFactoryKey = "nhibernate.current_session";

        /// <summary>
        ///     Maintains a dictionary of NHibernate session factories, one per database.  The key is 
        ///     the "factory key" used to look up the associated database, and used to decorate respective
        ///     repositories.  If only one database is being used, this dictionary contains a single
        ///     factory with a key of <see cref = "DefaultFactoryKey" />.
        /// </summary>
        private static readonly Dictionary<string, ISessionFactory> SessionFactories =
            new Dictionary<string, ISessionFactory>();

        private static IInterceptor registeredInterceptor;

        private static INHibernateConfigurationCache configurationCache;

        /// <summary>
        ///     Provides access to <see cref = "INHibernateConfigurationCache" /> object.
        /// </summary>
        /// <exception cref = "InvalidOperationException">
        /// Thrown on Set if the Init method has already been called and the 
        /// NHibernateSession.Storage property is not null.
        /// </exception>
        public static INHibernateConfigurationCache ConfigurationCache
        {
            get
            {
                return configurationCache;
            }

            set
            {
                if (Storage != null)
                {
                    throw new InvalidOperationException("Cannot set the ConfigurationCache property after calling Init");
                }

                configurationCache = value;
            }
        }

        /// <summary>
        ///     Used to get the current NHibernate session if you're communicating with a single database.
        ///     When communicating with multiple databases, invoke <see cref = "CurrentFor" /> instead.
        /// </summary>
        public static ISession Current
        {
            get
            {
                Check.Require(
                    !IsConfiguredForMultipleDatabases(), 
                    "The NHibernateSession.Current property may " +
                    "only be invoked if you only have one NHibernate session factory; i.e., you're " +
                    "only communicating with one database.  Since you're configured communications " +
                    "with multiple databases, you should instead call CurrentFor(string factoryKey)");

                return CurrentFor(DefaultFactoryKey);
            }
        }

        /// <summary>
        ///     An application-specific implementation of ISessionStorage must be setup either thru
        ///     <see cref = "InitStorage" /> or one of the <see cref = "Init" /> overloads.
        /// </summary>
        public static ISessionStorage Storage { get; set; }

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
            var configCache = ConfigurationCache;
            if (configCache != null)
            {
                config = configCache.LoadConfiguration(factoryKey, cfgFile, mappingAssemblies);
                if (config != null)
                {
                    return AddConfiguration(factoryKey, config.BuildSessionFactory(), config, validatorCfgFile);
                }
            }

            config = AddConfiguration(
                factoryKey, 
                mappingAssemblies, 
                autoPersistenceModel, 
                ConfigureNHibernate(cfgFile, cfgProperties), 
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

            return AddConfiguration(factoryKey, sessionFactory, cfg, validatorCfgFile);
        }

        public static Configuration AddConfiguration(
            string factoryKey, ISessionFactory sessionFactory, Configuration cfg, string validatorCfgFile)
        {
            Check.Require(
                !SessionFactories.ContainsKey(factoryKey), 
                "A session factory has already been configured with the key of " + factoryKey);

            SessionFactories.Add(factoryKey, sessionFactory);

            return cfg;
        }

        /// <summary>
        ///     This method is used by application-specific session storage implementations
        ///     and unit tests. Its job is to walk thru existing cached sessions and Close() each one.
        /// </summary>
        public static void CloseAllSessions()
        {
            if (Storage != null)
            {
                foreach (var session in Storage.GetAllSessions())
                {
                    if (session.IsOpen)
                    {
                        session.Close();
                    }
                }
            }
        }

        /// <summary>
        ///     Used to get the current NHibernate session associated with a factory key; i.e., the key 
        ///     associated with an NHibernate session factory for a specific database.
        /// 
        ///     If you're only communicating with one database, you should call <see cref = "Current" /> instead,
        ///     although you're certainly welcome to call this if you have the factory key available.
        /// </summary>
        public static ISession CurrentFor(string factoryKey)
        {
            Check.Require(!string.IsNullOrEmpty(factoryKey), "factoryKey may not be null or empty");
            Check.Require(Storage != null, "An ISessionStorage has not been configured");
            Check.Require(
                SessionFactories.ContainsKey(factoryKey), 
                "An ISessionFactory does not exist with a factory key of " + factoryKey);

            var session = Storage.GetSessionForKey(factoryKey);

            if (session == null)
            {
                if (registeredInterceptor != null)
                {
                    session = SessionFactories[factoryKey].OpenSession(registeredInterceptor);
                }
                else
                {
                    session = SessionFactories[factoryKey].OpenSession();
                }

                Storage.SetSessionForKey(factoryKey, session);
            }

            return session;
        }

        /// <summary>
        ///     Returns the default ISessionFactory using the DefaultFactoryKey.
        /// </summary>
        public static ISessionFactory GetDefaultSessionFactory()
        {
            return GetSessionFactoryFor(DefaultFactoryKey);
        }

        /// <summary>
        ///     Return an ISessionFactory based on the specified factoryKey.
        /// </summary>
        public static ISessionFactory GetSessionFactoryFor(string factoryKey)
        {
            if (!SessionFactories.ContainsKey(factoryKey))
            {
                return null;
            }

            return SessionFactories[factoryKey];
        }

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
            InitStorage(storage);
            try
            {
                return AddConfiguration(
                    DefaultFactoryKey, 
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
                Storage = null;
                throw;
            }
        }

        public static Configuration Init(Configuration configuration, ISessionStorage storage,
            Type[] baseEntityToIgnore,
            Type[] allEntities,
            Action<ModelMapper> autoMappingOverride,
            bool showLogs = false)
        {
            InitStorage(storage);

            try
            {
                var mapper = new ConventionModelMapper();

                DefineBaseClass(mapper, baseEntityToIgnore);
                mapper.AddAllManyToManyRelations(allEntities);
                mapper.ApplyNamingConventions();
                mapper.MapAllEnumsToStrings();
                if (autoMappingOverride != null) autoMappingOverride(mapper);

                var mapping = mapper.CompileMappingFor(allEntities);
                showOutputXmlMappings(mapping, showLogs, "mappings.xml");

                configuration.AddDeserializedMapping(mapping, null);
                var sessionFactory = configuration.BuildSessionFactory();

                return AddConfiguration(DefaultFactoryKey, sessionFactory, configuration, string.Empty);
            }
            catch
            {
                // If this NHibernate config throws an exception, null the Storage reference so 
                // the config can be corrected without having to restart the web application.
                Storage = null;
                throw;
            }
        }

        public static Configuration Init(SimpleSessionStorage storage, 
            string connectionString, Assembly mappingsAssembly, string mappingsNamespace, 
            string validationDefinitionsNamespace, bool showLogs, string outputXmlMappingsFile, 
            Type baseEntityToIgnore, bool mapAllEnumsToStrings, Action<ModelMapper> autoMappingOverride)
        {
            InitStorage(storage);

            try
            {
                var configuration = ReadConfigFromCacheFileOrBuildIt(mappingsAssembly, connectionString, showLogs, 
                    baseEntityToIgnore, mappingsNamespace, mapAllEnumsToStrings, autoMappingOverride, outputXmlMappingsFile, validationDefinitionsNamespace);
                var sessionFactory = configuration.BuildSessionFactory();

                return AddConfiguration(DefaultFactoryKey, sessionFactory, configuration, string.Empty);
            }
            catch
            {
                // If this NHibernate config throws an exception, null the Storage reference so 
                // the config can be corrected without having to restart the web application.
                Storage = null;
                throw;
            }
        }

        private static Configuration ReadConfigFromCacheFileOrBuildIt(Assembly mappingsAssembly, string connectionString, bool showLogs, 
            Type baseEntityToIgnore, string mappingsNamespace, bool mapAllEnumsToStrings, Action<ModelMapper> autoMappingOverride,
            string outputXmlMappingsFile, string validationDefinitionsNamespace)
        {
            Configuration nhConfigurationCache;
            var nhCfgCache = new ConfigurationFileCache(mappingsAssembly);
            var cachedCfg = nhCfgCache.LoadConfigurationFromFile();
            if (cachedCfg == null)
            {
                nhConfigurationCache = BuildConfiguration(connectionString, showLogs, baseEntityToIgnore, mappingsAssembly,
                    mappingsNamespace, mapAllEnumsToStrings, autoMappingOverride, outputXmlMappingsFile, validationDefinitionsNamespace);
                nhCfgCache.SaveConfigurationToFile(nhConfigurationCache);
            }
            else
            {
                nhConfigurationCache = cachedCfg;
            }
            return nhConfigurationCache;
        }

        private static Configuration BuildConfiguration(string connectionString, bool showLogs, Type baseEntityToIgnore, 
            Assembly mappingsAssembly, string mappingsNamespace, bool mapAllEnumsToStrings, 
            Action<ModelMapper> autoMappingOverride, string outputXmlMappingsFile, string validationDefinitionsNamespace)
        {
            var config = InitConfiguration(connectionString, showLogs);
            var mapping = GetMappings(baseEntityToIgnore, mappingsAssembly, mappingsNamespace, mapAllEnumsToStrings, autoMappingOverride, showLogs, outputXmlMappingsFile);
            config.AddDeserializedMapping(mapping, "NHSchemaTest");
            InjectValidationAndFieldLengths(config, validationDefinitionsNamespace, mappingsAssembly);
            return config;
        }

        private static Configuration InitConfiguration(string connectionString, bool showLogs)
        {
            var configure = new Configuration();
            configure.SessionFactoryName("BuildIt");

            configure.DataBaseIntegration(db =>
            {
                db.ConnectionProvider<DriverConnectionProvider>();
                db.Dialect<SQLiteDialect>();
                db.Driver<SQLite20Driver>();
                db.KeywordsAutoImport = Hbm2DDLKeyWords.AutoQuote;
                db.IsolationLevel = IsolationLevel.ReadCommitted;
                db.ConnectionString = connectionString;
                db.Timeout = 10;
                db.BatchSize = 20;

                if (showLogs)
                {
                    db.LogFormattedSql = true;
                    db.LogSqlInConsole = true;
                    db.AutoCommentSql = false;
                }
            });

            return configure;
        }

        private static HbmMapping GetMappings(Type baseEntityToIgnore, Assembly mappingsAssembly, string mappingsNamespace, 
            bool mapAllEnumsToStrings, Action<ModelMapper> autoMappingOverride, bool showLogs, string outputXmlMappingsFile)
        {
            //Using the built-in auto-mapper
            var mapper = new ConventionModelMapper();
            DefineBaseClass(mapper, new[] { baseEntityToIgnore });
            var allEntities = mappingsAssembly.GetTypes().Where(t => t.Namespace == mappingsNamespace).ToList();
            mapper.AddAllManyToManyRelations(allEntities);
            mapper.ApplyNamingConventions();
            if (mapAllEnumsToStrings) mapper.MapAllEnumsToStrings();
            if (autoMappingOverride != null) autoMappingOverride(mapper);

            var mapping = mapper.CompileMappingFor(allEntities);
            showOutputXmlMappings(mapping, showLogs, outputXmlMappingsFile);
            return mapping;
        }

        private static void DefineBaseClass(ConventionModelMapper mapper, Type[] baseEntityToIgnore)
        {
            if (baseEntityToIgnore == null) return;
            mapper.IsEntity((type, declared) =>
                baseEntityToIgnore.Any(x => x.IsAssignableFrom(type)) &&
                !baseEntityToIgnore.Any(x => x == type) &&
                !type.IsInterface);
            mapper.IsRootEntity((type, declared) => baseEntityToIgnore.Any(x => x == type.BaseType));
        }

        private static void showOutputXmlMappings(HbmMapping mapping, bool showLogs, string outputXmlMappingsFile)
        {
            if (!showLogs) return;
            var outputXmlMappings = mapping.AsString();
            Console.WriteLine(outputXmlMappings);
            var path = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), outputXmlMappingsFile);
            File.WriteAllText(path, outputXmlMappings);
        }

        private static void InjectValidationAndFieldLengths(Configuration nhConfig, string validationDefinitionsNamespace, Assembly mappingsAssembly)
        {
            if (string.IsNullOrWhiteSpace(validationDefinitionsNamespace))
                return;

            var mappingsValidatorEngine = new ValidatorEngine();
            var configuration = new global::NHibernate.Validator.Cfg.Loquacious.FluentConfiguration();
            var validationDefinitions = mappingsAssembly.GetTypes()
                                                        .Where(t => t.Namespace == validationDefinitionsNamespace)
                                                        .ValidationDefinitions();
            configuration
                    .Register(validationDefinitions)
                    .SetDefaultValidatorMode(ValidatorMode.OverrideExternalWithAttribute)
                    .IntegrateWithNHibernate
                    .ApplyingDDLConstraints()
                    .And
                    .RegisteringListeners();

            mappingsValidatorEngine.Configure(configuration);

            //Registering of Listeners and DDL-applying here
            nhConfig.Initialize(mappingsValidatorEngine);
        }

        public static void InitStorage(ISessionStorage storage)
        {
            Check.Require(storage != null, "storage mechanism was null but must be provided");
            Check.Require(Storage == null, "A storage mechanism has already been configured for this application");
            Storage = storage;
        }

        public static bool IsConfiguredForMultipleDatabases()
        {
            return SessionFactories.Count > 1;
        }

        public static void RegisterInterceptor(IInterceptor interceptor)
        {
            Check.Require(interceptor != null, "interceptor may not be null");

            registeredInterceptor = interceptor;
        }

        public static void RemoveSessionFactoryFor(string factoryKey)
        {
            if (GetSessionFactoryFor(factoryKey) != null)
            {
                SessionFactories.Remove(factoryKey);
            }
        }

        /// <summary>
        ///     To facilitate unit testing, this method will reset this object back to its
        ///     original state before it was configured.
        /// </summary>
        public static void Reset()
        {
            if (Storage != null)
            {
                foreach (var session in Storage.GetAllSessions())
                {
                    session.Dispose();
                }
            }

            SessionFactories.Clear();

            Storage = null;
            registeredInterceptor = null;
            ConfigurationCache = null;
        }

        private static Configuration ConfigureNHibernate(string cfgFile, IDictionary<string, string> cfgProperties)
        {
            var cfg = new Configuration();

            if (cfgProperties != null)
            {
                cfg.AddProperties(cfgProperties);
            }

            if (string.IsNullOrEmpty(cfgFile) == false)
            {
                return cfg.Configure(cfgFile);
            }

            if (File.Exists("Hibernate.cfg.xml"))
            {
                return cfg.Configure();
            }

            return cfg;
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