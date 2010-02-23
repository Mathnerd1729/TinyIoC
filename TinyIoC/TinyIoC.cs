﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace TinyIoC
{
    #region Exceptions
    public class TinyIoCResolutionException : Exception
    {
        private const string ERROR_TEXT = "Unable to resolve type: {0}";

        public TinyIoCResolutionException(Type type)
            : base(String.Format(ERROR_TEXT, type.FullName))
        {
        }

        public TinyIoCResolutionException(Type type, Exception innerException)
            : base(String.Format(ERROR_TEXT, type.FullName), innerException)
        {
        }
    }

    public class TinyIoCInstantiationTypeException : Exception
    {
        private const string ERROR_TEXT = "Unable to make {0} {1}";

        public TinyIoCInstantiationTypeException(Type type, string method)
            : base(String.Format(ERROR_TEXT, type.FullName, method))
        {
        }

        public TinyIoCInstantiationTypeException(Type type, string method, Exception innerException)
            : base(String.Format(ERROR_TEXT, type.FullName, method), innerException)
        {
        }
    }
    #endregion

    public sealed class TinyIoC : IDisposable
    {
        #region Public Setup / Settings Classes
        /// <summary>
        /// Name/Value pairs for specifying "user" parameters when resolving
        /// </summary>
        public sealed class NamedParameterOverloads : Dictionary<string, object>
        {
        }

        /// <summary>
        /// Resolution settings
        /// </summary>
        public sealed class ResolutionOptions
        {
            /// <summary>
            /// Whether to attempt to resolve unregistered types
            /// </summary>
            public bool IncludeUnregistered { get; set; }

            public ResolutionOptions()
                : this(true)
            {
            }

            public ResolutionOptions(bool includeUnregistered)
            {
                IncludeUnregistered = includeUnregistered;
            }

            public static ResolutionOptions GetDefault()
            {
                return new ResolutionOptions();
            }
        }

        /// <summary>
        /// Registration options for "fluent" API
        /// </summary>
        /// <typeparam name="RegisterType">Registered type</typeparam>
        /// <typeparam name="RegisterImplementation">Implementation type for construction of RegisteredType</typeparam>
        public sealed class RegisterOptions<RegisterType, RegisterImplementation>
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            private TinyIoC _Container;

            public RegisterOptions(TinyIoC container)
            {
                _Container = container;
            }

            public RegisterOptions<RegisterType, RegisterImplementation> AsSingleton()
            {
                var currentFactory = _Container.GetCurrentFactory<RegisterType>();

                if (currentFactory == null)
                    throw new TinyIoCInstantiationTypeException(typeof(RegisterType), "singleton");

                return _Container.AddUpdateRegistration<RegisterType, RegisterImplementation>(currentFactory.SingletonVariant);
            }

            public RegisterOptions<RegisterType, RegisterImplementation> AsMultiInstance()
            {
                var currentFactory = _Container.GetCurrentFactory<RegisterType>();

                if (currentFactory == null)
                    throw new TinyIoCInstantiationTypeException(typeof(RegisterType), "multi-instance");

                return _Container.AddUpdateRegistration<RegisterType, RegisterImplementation>(currentFactory.MultiInstanceVariant);
            }

            public RegisterOptions<RegisterType, RegisterImplementation> WithWeakReference()
            {
                var currentFactory = _Container.GetCurrentFactory<RegisterType>();

                if (currentFactory == null)
                    throw new TinyIoCInstantiationTypeException(typeof(RegisterType), "weak reference");

                return _Container.AddUpdateRegistration<RegisterType, RegisterImplementation>(currentFactory.WeakReferenceVariant);
            }

            public RegisterOptions<RegisterType, RegisterImplementation> WithStrongReference()
            {
                var currentFactory = _Container.GetCurrentFactory<RegisterType>();

                if (currentFactory == null)
                    throw new TinyIoCInstantiationTypeException(typeof(RegisterType), "strong reference");

                return _Container.AddUpdateRegistration<RegisterType, RegisterImplementation>(currentFactory.StrongReferenceVariant);
            }
        }
        #endregion

        #region Public API
        #region Registration
        /// <summary>
        /// Creates/replaces a container class registration with default options.
        /// </summary>
        /// <typeparam name="RegisterImplementation">Type to register</typeparam>
        /// <returns>RegisterOptions for fluent API</returns>
        public RegisterOptions<RegisterImplementation, RegisterImplementation> Register<RegisterImplementation>()
            where RegisterImplementation : class
        {
            return Register<RegisterImplementation, RegisterImplementation>();
        }

        /// <summary>
        /// Creates/replaces a named container class registration with default options.
        /// </summary>
        /// <typeparam name="RegisterImplementation">Type to register</typeparam>
        /// <param name="name">Name of registration</param>
        /// <returns>RegisterOptions for fluent API</returns>
        public RegisterOptions<RegisterImplementation, RegisterImplementation> Register<RegisterImplementation>(string name)
            where RegisterImplementation : class
        {
            return Register<RegisterImplementation, RegisterImplementation>(name);
        }

        /// <summary>
        /// Creates/replaces a container class registration with a given implementation and default options.
        /// </summary>
        /// <typeparam name="RegisterType">Type to register</typeparam>
        /// <typeparam name="RegisterImplementation">Type to instantiate that implements RegisterType</typeparam>
        /// <returns>RegisterOptions for fluent API</returns>
        public RegisterOptions<RegisterType, RegisterImplementation> Register<RegisterType, RegisterImplementation>()
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            return AddUpdateRegistration<RegisterType, RegisterImplementation>(GetDefaultObjectFactory<RegisterType, RegisterImplementation>());
        }

        /// <summary>
        /// Creates/replaces a named container class registration with a given implementation and default options.
        /// </summary>
        /// <typeparam name="RegisterType">Type to register</typeparam>
        /// <typeparam name="RegisterImplementation">Type to instantiate that implements RegisterType</typeparam>
        /// <param name="name">Name of registration</param>
        /// <returns>RegisterOptions for fluent API</returns>
        public RegisterOptions<RegisterType, RegisterImplementation> Register<RegisterType, RegisterImplementation>(string name)
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            return AddUpdateRegistration<RegisterType, RegisterImplementation>(GetDefaultObjectFactory<RegisterType, RegisterImplementation>(), name);
        }

        /// <summary>
        /// Creates/replaces a container class registration with a specific, strong referenced, instance.
        /// </summary>
        /// <typeparam name="RegisterImplementation">Type to register</typeparam>
        /// <param name="instance">Instance of RegisterType to register</param>
        /// <returns>RegisterOptions for fluent API</returns>
        public RegisterOptions<RegisterType, RegisterType> Register<RegisterType>(RegisterType instance)
           where RegisterType : class
        {
            return AddUpdateRegistration<RegisterType, RegisterType>(new InstanceFactory<RegisterType, RegisterType>(instance));
        }

        /// <summary>
        /// Creates/replaces a named container class registration with a specific, strong referenced, instance.
        /// </summary>
        /// <typeparam name="RegisterImplementation">Type to register</typeparam>
        /// <param name="instance">Instance of RegisterType to register</param>
        /// <param name="name">Name of registration</param>
        /// <returns>RegisterOptions for fluent API</returns>
        public RegisterOptions<RegisterType, RegisterType> Register<RegisterType>(RegisterType instance, string name)
            where RegisterType : class
        {
            return AddUpdateRegistration<RegisterType, RegisterType>(new InstanceFactory<RegisterType, RegisterType>(instance), name);
        }

        /// <summary>
        /// Creates/replaces a container class registration with a user specified factory
        /// </summary>
        /// <typeparam name="RegisterType">Type to register</typeparam>
        /// <param name="factory">Factory/lambda that returns an instance of RegisterType</param>
        /// <returns>RegisterOptions for fluent API</returns>
        public RegisterOptions<RegisterType, RegisterType> Register<RegisterType>(Func<TinyIoC, NamedParameterOverloads, RegisterType> factory)
            where RegisterType : class
        {
            return AddUpdateRegistration<RegisterType, RegisterType>(new DelegateFactory<RegisterType>(factory));
        }

        /// <summary>
        /// Creates/replaces a named container class registration with a user specified factory
        /// </summary>
        /// <typeparam name="RegisterType">Type to register</typeparam>
        /// <param name="factory">Factory/lambda that returns an instance of RegisterType</param>
        /// <param name="name">Name of registation</param>
        /// <returns>RegisterOptions for fluent API</returns>
        public RegisterOptions<RegisterType, RegisterType> Register<RegisterType>(Func<TinyIoC, NamedParameterOverloads, RegisterType> factory, string name)
            where RegisterType : class
        {
            return AddUpdateRegistration<RegisterType, RegisterType>(new DelegateFactory<RegisterType>(factory), name);
        }
        #endregion

        #region Resolution
        /// <summary>
        /// Attempts to resolve a type using default options.
        /// </summary>
        /// <typeparam name="RegisterType">Type to resolve</typeparam>
        /// <returns>Instance of type</returns>
        /// <exception cref="TinyIoCResolutionException">Unable to resolve the type.</exception>
        public RegisterType Resolve<RegisterType>()
            where RegisterType : class
        {
            return Resolve<RegisterType>(new NamedParameterOverloads(), string.Empty);
        }

        /// <summary>
        /// Attempts to resolve a type using default options and the supplied name.
        ///
        /// Parameters are used in conjunction with normal container resolution to find the most suitable constructor (if one exists).
        /// All user supplied parameters must exist in at least one resolvable constructor of RegisterType or resolution will fail.
        /// </summary>
        /// <typeparam name="RegisterType">Type to resolve</typeparam>
        /// <param name="name">Name of registration</param>
        /// <returns>Instance of type</returns>
        /// <exception cref="TinyIoCResolutionException">Unable to resolve the type.</exception>
        public RegisterType Resolve<RegisterType>(string name)
            where RegisterType : class
        {
            return (Resolve(typeof(RegisterType), name) as RegisterType);
        }

        /// <summary>
        /// Attempts to resolve a type using default options and the supplied constructor parameters.
        ///
        /// Parameters are used in conjunction with normal container resolution to find the most suitable constructor (if one exists).
        /// All user supplied parameters must exist in at least one resolvable constructor of RegisterType or resolution will fail.
        /// </summary>
        /// <typeparam name="RegisterType">Type to resolve</typeparam>
        /// <param name="parameters">User specified constructor parameters</param>
        /// <returns>Instance of type</returns>
        /// <exception cref="TinyIoCResolutionException">Unable to resolve the type.</exception>
        public RegisterType Resolve<RegisterType>(NamedParameterOverloads parameters)
            where RegisterType : class
        {
            return (Resolve(typeof(RegisterType), parameters) as RegisterType);
        }

        /// <summary>
        /// Attempts to resolve a type using default options and the supplied constructor parameters and name.
        ///
        /// Parameters are used in conjunction with normal container resolution to find the most suitable constructor (if one exists).
        /// All user supplied parameters must exist in at least one resolvable constructor of RegisterType or resolution will fail.
        /// </summary>
        /// <typeparam name="RegisterType">Type to resolve</typeparam>
        /// <param name="parameters">User specified constructor parameters</param>
        /// <param name="name">Name of registration</param>
        /// <returns>Instance of type</returns>
        /// <exception cref="TinyIoCResolutionException">Unable to resolve the type.</exception>
        public RegisterType Resolve<RegisterType>(NamedParameterOverloads parameters, string name)
            where RegisterType : class
        {
            return (Resolve(typeof(RegisterType), parameters, name) as RegisterType);
        }

        /// <summary>
        /// Attempts to predict whether a given type can be resolved.
        ///
        /// Note: Resolution may still fail if user defined factory registations fail to construct objects when called.
        /// </summary>
        /// <typeparam name="ResolveType">Type to resolve</typeparam>
        /// <returns>Instance of ResolveType</returns>
        public bool CanResolve<ResolveType>()
        {
            return CanResolve(typeof(ResolveType));
        }

        /// <summary>
        /// Attempts to predict whether a given type can be resolved with the supplied constructor parameters.
        ///
        /// Parameters are used in conjunction with normal container resolution to find the most suitable constructor (if one exists).
        /// All user supplied parameters must exist in at least one resolvable constructor of RegisterType or resolution will fail.
        /// 
        /// Note: Resolution may still fail if user defined factory registations fail to construct objects when called.
        /// </summary>
        /// <typeparam name="ResolveType">Type to resolve</typeparam>
        /// <returns>Instance of ResolveType</returns>
        public bool CanResolve<ResolveType>(NamedParameterOverloads parameters)
        {
            return CanResolve(typeof(ResolveType), parameters);
        }
        #endregion
        #endregion

        #region Object Factories
        private abstract class ObjectFactoryBase
        {
            /// <summary>
            /// Whether to assume this factory sucessfully constructs its objects
            /// 
            /// Generally set to true for delegate style factories as CanResolve cannot delve
            /// into the delegates they contain.
            /// </summary>
            public virtual bool AssumeConstruction { get { return false; } }

            /// <summary>
            /// The type the factory instantiates
            /// </summary>
            public abstract Type CreatesType { get; }

            /// <summary>
            /// Create the type
            /// </summary>
            /// <param name="container">Container that requested the creation</param>
            /// <param name="parameters">Any user parameters passed</param>
            /// <returns></returns>
            public abstract object GetObject(TinyIoC container, NamedParameterOverloads parameters);

            public virtual ObjectFactoryBase SingletonVariant
            {
                get
                {
                    throw new TinyIoCInstantiationTypeException(this.GetType(), "singleton");
                }
            }

            public virtual ObjectFactoryBase MultiInstanceVariant
            {
                get
                {
                    throw new TinyIoCInstantiationTypeException(this.GetType(), "multi-instance");
                }
            }

            public virtual ObjectFactoryBase StrongReferenceVariant
            {
                get
                {
                    throw new TinyIoCInstantiationTypeException(this.GetType(), "strong reference");
                }
            }

            public virtual ObjectFactoryBase WeakReferenceVariant
            {
                get
                {
                    throw new TinyIoCInstantiationTypeException(this.GetType(), "weak reference");
                }
            }
        }

        /// <summary>
        /// IObjectFactory that creates new instances of types for each resolution
        /// </summary>
        /// <typeparam name="RegisterType">Registered type</typeparam>
        /// <typeparam name="RegisterImplementation">Type to construct to fullful request for RegisteredType</typeparam>
        private class NewInstanceFactory<RegisterType, RegisterImplementation> : ObjectFactoryBase
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            public override Type CreatesType { get { return typeof(RegisterImplementation); } }

            public override object GetObject(TinyIoC container, NamedParameterOverloads parameters)
            {
                try
                {
                    return container.ConstructType(typeof(RegisterImplementation), parameters);
                }
                catch (TinyIoCResolutionException ex)
                {
                    throw new TinyIoCResolutionException(typeof(RegisterImplementation), ex);
                }
            }

            public override ObjectFactoryBase SingletonVariant
            {
                get
                {
                    return new SingletonFactory<RegisterType, RegisterImplementation>();
                }
            }
        }

        /// <summary>
        /// IObjectFactory that invokes a specified delegate to construct the object
        /// </summary>
        /// <typeparam name="RegisterType">Registered type to be constructed</typeparam>
        private class DelegateFactory<RegisterType> : ObjectFactoryBase
            where RegisterType : class
        {
            private Func<TinyIoC, NamedParameterOverloads, RegisterType> _Factory;

            public override bool AssumeConstruction { get { return true; } }

            public override Type CreatesType { get { return typeof(RegisterType); } }

            public override object GetObject(TinyIoC container, NamedParameterOverloads parameters)
            {
                try
                {
                    return _Factory.Invoke(container, parameters);
                }
                catch (Exception ex)
                {
                    throw new TinyIoCResolutionException(typeof(RegisterType), ex);
                }
            }

            public DelegateFactory(Func<TinyIoC, NamedParameterOverloads, RegisterType> factory)
            {
                if (factory == null)
                    throw new ArgumentNullException("factory");

                _Factory = factory;
            }
        }

        /// <summary>
        /// Stores an particular instance to return for a type
        /// </summary>
        /// <typeparam name="RegisterType">Registered type</typeparam>
        /// <typeparam name="RegisterImplementation">Type of the instance</typeparam>
        private class InstanceFactory<RegisterType, RegisterImplementation> : ObjectFactoryBase, IDisposable
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            private RegisterImplementation instance;

            public override Type CreatesType
            {
                get { return typeof(RegisterImplementation); }
            }

            public override object GetObject(TinyIoC container, NamedParameterOverloads parameters)
            {
                return instance;
            }

            public InstanceFactory(RegisterImplementation instance)
            {
                this.instance = instance;
            }

            public void Dispose()
            {
                var disposable = instance as IDisposable;

                if (disposable != null)
                    disposable.Dispose();
            }
        }

        /// <summary>
        /// A factory that lazy instantiates a type and always returns the same instance
        /// </summary>
        /// <typeparam name="RegisterType">Registered type</typeparam>
        /// <typeparam name="RegisterImplementation">Type to instantiate</typeparam>
        private class SingletonFactory<RegisterType, RegisterImplementation> : ObjectFactoryBase, IDisposable
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            private readonly object SingletonLock = new object();
            private RegisterImplementation _Current;

            public override Type CreatesType
            {
                get { return typeof(RegisterImplementation); }
            }

            public override object GetObject(TinyIoC container, NamedParameterOverloads parameters)
            {
                if (parameters.Count != 0)
                    throw new ArgumentException("Cannot specify parameters for singleton types");

                // TODO - Better singleton implementation? Maybe ditch lazy instantiation or always lock rather than double if
                if (_Current == null)
                    lock (SingletonLock)
                        if (_Current == null)
                            _Current = container.ConstructType(typeof(RegisterImplementation)) as RegisterImplementation;

                return _Current;
            }

            public SingletonFactory(RegisterImplementation instance)
            {
                _Current = instance;
            }

            public SingletonFactory()
            {

            }

            public override ObjectFactoryBase MultiInstanceVariant
            {
                get
                {
                    return new NewInstanceFactory<RegisterType, RegisterImplementation>();
                }
            }

            public void Dispose()
            {
                if (_Current != null)
                {
                    var disposable = _Current as IDisposable;

                    if (disposable != null)
                        disposable.Dispose();
                }
            }
        }
        #endregion

        #region Singleton Container
        private static readonly TinyIoC _Current = new TinyIoC();

        static TinyIoC()
        {
        }

        /// <summary>
        /// Lazy created Singleton instance of the container for simple scenarios
        /// </summary>
        public static TinyIoC Current
        {
            get
            {
                return _Current;
            }
        }
        #endregion

        #region Type Registrations
        private sealed class TypeRegistration
        {
            public Type Type { get; private set; }
            public string Name { get; private set; }

            public TypeRegistration(Type type)
                : this(type, string.Empty)
            {
            }

            public TypeRegistration(Type type, string name)
            {
                Type = type;
                Name = name;
            }

            public override bool Equals(object obj)
            {
                var typeRegistration = obj as TypeRegistration;

                if (obj == null)
                    return false;

                if (this.Type != typeRegistration.Type)
                    return false;

                if (String.Compare(this.Name, typeRegistration.Name, true) != 0)
                    return false;

                return true;
            }

            public override int GetHashCode()
            {
                return String.Format("{0}|{1}", this.Type.FullName, this.Name).GetHashCode();
            }
        }
        private readonly Dictionary<TypeRegistration, ObjectFactoryBase> _RegisteredTypes;
        #endregion

        #region Constructors
        public TinyIoC()
        {
            _RegisteredTypes = new Dictionary<TypeRegistration, ObjectFactoryBase>();

            RegisterDefaultTypes();
        }
        #endregion

        #region Utility Methods
        private void RegisterDefaultTypes()
        {
            this.Register<TinyIoC>(this);
        }

        private ObjectFactoryBase GetCurrentFactory<RegisterType>()
        {
            ObjectFactoryBase current = null;

            _RegisteredTypes.TryGetValue(new TypeRegistration(typeof(RegisterType)), out current);

            return current;
        }

        private RegisterOptions<RegisterType, RegisterImplementation> AddUpdateRegistration<RegisterType, RegisterImplementation>(ObjectFactoryBase factory)
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            return AddUpdateRegistration<RegisterType, RegisterImplementation>(factory, string.Empty);
        }

        private RegisterOptions<RegisterType, RegisterImplementation> AddUpdateRegistration<RegisterType, RegisterImplementation>(ObjectFactoryBase factory, string name)
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            ObjectFactoryBase current;

            var typeRegistration = new TypeRegistration(typeof(RegisterType), name);

            if (_RegisteredTypes.TryGetValue(typeRegistration, out current))
            {
                var disposable = current as IDisposable;

                if (disposable != null)
                    disposable.Dispose();
            }

            _RegisteredTypes[typeRegistration] = factory;

            return new RegisterOptions<RegisterType, RegisterImplementation>(this);
        }

        private static ObjectFactoryBase GetDefaultObjectFactory<RegisterType, RegisterImplementation>()
            where RegisterType : class
            where RegisterImplementation : class, RegisterType
        {
            if (typeof(RegisterType).IsInterface)
                return new SingletonFactory<RegisterType, RegisterImplementation>();

            return new NewInstanceFactory<RegisterType, RegisterImplementation>();
        }

        private bool CanResolve(Type type)
        {
            return CanResolve(type, new NamedParameterOverloads());
        }

        private bool CanResolve(Type type, NamedParameterOverloads parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            Type checkType = type;

            ObjectFactoryBase factory;
            if (_RegisteredTypes.TryGetValue(new TypeRegistration(checkType), out factory))
            {
                if (factory.AssumeConstruction)
                    return true;

                checkType = factory.CreatesType;
            }

            return (GetBestConstructor(checkType, parameters) != null) ? true : false;
        }

        private object Resolve(Type type)
        {
            return Resolve(type, new NamedParameterOverloads(), string.Empty);
        }

        private object Resolve(Type type, NamedParameterOverloads parameters)
        {
            return Resolve(type, parameters, string.Empty);
        }

        private object Resolve(Type type, string name)
        {
            return Resolve(type, new NamedParameterOverloads(), name);
        }

        private object Resolve(Type type, NamedParameterOverloads parameters, string name)
        {
            ObjectFactoryBase factory;

            if (_RegisteredTypes.TryGetValue(new TypeRegistration(type, name), out factory))
            {
                return factory.GetObject(this, parameters);
            }
            else
            {
                // TODO - check options
                if (type.IsAbstract || type.IsInterface)
                    throw new TinyIoCResolutionException(type);
                else
                {
                    try
                    {
                        return ConstructType(type, parameters);
                    }
                    catch (TinyIoCResolutionException ex)
                    {
                        throw new TinyIoCResolutionException(type, ex);
                    }
                }

            }
        }

        private bool CanConstruct(ConstructorInfo ctor, NamedParameterOverloads parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            foreach (var parameter in ctor.GetParameters())
            {
                if (!parameters.ContainsKey(parameter.Name) && !CanResolve(parameter.ParameterType))
                    return false;
            }

            return true;
        }

        private ConstructorInfo GetBestConstructor(Type type, NamedParameterOverloads parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            var ctors = from ctor in type.GetConstructors()
                        orderby ctor.GetParameters().Count()
                        select ctor;

            foreach (var ctor in ctors)
            {
                if (CanConstruct(ctor, parameters))
                    return ctor;
            }

            return null;
        }

        private object ConstructType(Type type)
        {
            return ConstructType(type, new NamedParameterOverloads());
        }

        private object ConstructType(Type type, NamedParameterOverloads parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            var ctor = GetBestConstructor(type, parameters);
            if (ctor == null)
                throw new TinyIoCResolutionException(type);

            var ctorParams = ctor.GetParameters();
            object[] args = new object[ctorParams.Count()];

            for (int parameterIndex = 0; parameterIndex < ctorParams.Count() - 1; parameterIndex++)
            {
                var currentParam = ctorParams[parameterIndex];

                args[parameterIndex] = parameters.ContainsKey(currentParam.Name) ? parameters[currentParam.Name] : Resolve(currentParam.ParameterType);
            }
            
            try
            {
                return Activator.CreateInstance(type, args);
            }
            catch (Exception ex)
            {
                throw new TinyIoCResolutionException(type, ex);
            }
        }

        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            var disposableFactories = from factory in _RegisteredTypes.Values
                                      where factory is IDisposable
                                      select factory as IDisposable;

            foreach (var factory in disposableFactories)
            {
                factory.Dispose();
            }

            _RegisteredTypes.Clear();
        }

        #endregion
    }
}
