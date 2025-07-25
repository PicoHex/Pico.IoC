﻿namespace Pico.IoC.Test;

public class DependencyInjectionLifecycleTests : IDisposable
{
    private readonly ISvcContainer _container;
    private readonly ISvcProvider _provider;

    public DependencyInjectionLifecycleTests()
    {
        _container = Bootstrap.CreateContainer();
        _provider = _container.GetProvider();
    }

    public void Dispose()
    {
        (_container as IDisposable)?.Dispose();
    }

    #region Transient Lifecycle Tests
    [Fact]
    public void Transient_Registration_ShouldCreateNewInstances()
    {
        // Arrange
        _container.RegisterTransient<IService, ServiceImpl>();

        // Act
        var instance1 = _provider.Resolve<IService>();
        var instance2 = _provider.Resolve<IService>();

        // Assert
        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void Transient_WithDependencies_ShouldResolveCorrectly()
    {
        // Arrange
        _container.RegisterTransient<IService, ServiceImpl>();
        _container.RegisterTransient<IServiceA, ServiceAImpl>();
        _container.RegisterTransient<IServiceB, ServiceBImpl>();

        // Act
        var serviceA = _provider.Resolve<IServiceA>();
        var serviceB = _provider.Resolve<IServiceB>();

        // Assert
        Assert.NotNull(serviceA);
        Assert.NotNull(serviceB);
        Assert.IsType<ServiceAImpl>(serviceA);
        Assert.IsType<ServiceBImpl>(serviceB);
    }
    #endregion

    #region Singleton Lifecycle Tests
    [Fact]
    public void Singleton_Registration_ShouldReturnSameInstance()
    {
        // Arrange
        _container.RegisterSingle<IService, ServiceImpl>();

        // Act
        var instance1 = _provider.Resolve<IService>();
        var instance2 = _provider.Resolve<IService>();

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Singleton_AcrossScopes_ShouldMaintainSingleInstance()
    {
        // Arrange
        _container.RegisterSingle<IService, ServiceImpl>();

        // Act
        var rootInstance = _provider.Resolve<IService>();
        IService scopedInstance;

        using (var scope = _provider.CreateScope())
        {
            scopedInstance = scope.Resolve<IService>();
        }

        // Assert
        Assert.Same(rootInstance, scopedInstance);
    }
    #endregion

    #region Scoped Lifecycle Tests
    [Fact]
    public void Scoped_Registration_ShouldCreateInstancePerScope()
    {
        // Arrange
        _container.RegisterScoped<IService, ServiceImpl>();

        // Act
        IService scope1Instance1,
            scope1Instance2,
            scope2Instance;

        using (var scope1 = _provider.CreateScope())
        {
            scope1Instance1 = scope1.Resolve<IService>();
            scope1Instance2 = scope1.Resolve<IService>();
        }

        using (var scope2 = _provider.CreateScope())
        {
            scope2Instance = scope2.Resolve<IService>();
        }

        // Assert
        Assert.Same(scope1Instance1, scope1Instance2);
        Assert.NotSame(scope1Instance1, scope2Instance);
    }

    [Fact]
    public async Task Scoped_AsyncDisposable_ShouldWorkCorrectly()
    {
        // Arrange
        _container.RegisterScoped<IService, ServiceImpl>();

        // Act
        IService scope1Instance;
        await using (var scope1 = _provider.CreateScope())
        {
            scope1Instance = scope1.Resolve<IService>();
            var tempInstance = scope1.Resolve<IService>();
            Assert.Same(scope1Instance, tempInstance);
        }

        await using (var scope2 = _provider.CreateScope())
        {
            var scope2Instance = scope2.Resolve<IService>();
            Assert.NotSame(scope1Instance, scope2Instance);
        }
    }
    #endregion

    #region Error Handling Tests
    [Fact]
    public void Resolve_UnregisteredService_ShouldThrowException()
    {
        Assert.Throws<ServiceNotRegisteredException>(
            () => _provider.Resolve<INonExistingService>()
        );
    }

    [Fact]
    public void Circular_Dependencies_ShouldThrowException()
    {
        // Arrange
        _container.RegisterTransient<INode1, Node1>();
        _container.RegisterTransient<INode2, Node2>();
        _container.RegisterTransient<INode3, Node3>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _provider.Resolve<INode1>());
    }
    #endregion

    #region Advanced Scenarios
    [Fact]
    public void Mixed_Lifecycles_ShouldResolveCorrectly()
    {
        // Arrange
        _container.RegisterTransient<IService, ServiceImpl>();
        _container.RegisterSingle<IServiceA, ServiceAImpl>();
        _container.RegisterScoped<IServiceB, ServiceBImpl>();
        _container.RegisterScoped<IServiceC, ServiceCImpl>();

        // Act
        var singleton1 = _provider.Resolve<IServiceA>();
        var singleton2 = _provider.Resolve<IServiceA>();

        IServiceA scoped1,
            scoped2;
        IServiceC innerScope1,
            innerScope2;
        using (var scope = _provider.CreateScope())
        {
            scoped1 = scope.Resolve<IServiceA>();
            scoped2 = scope.Resolve<IServiceA>();

            using (var innerScope = scope.CreateScope())
            {
                innerScope1 = innerScope.Resolve<IServiceC>();
                innerScope2 = innerScope.Resolve<IServiceC>();
            }
        }

        // Assert
        Assert.Same(singleton1, singleton2);
        Assert.Same(scoped1, scoped2);
        Assert.Same(innerScope1, innerScope2);
    }

    [Fact]
    public void Runtime_Registration_ShouldWorkInTransient()
    {
        // Arrange
        _container.RegisterTransient<IService, ServiceImpl>();
        _container.RegisterTransient<IServiceA, ServiceAImpl>();
        _container.RegisterTransient<IServiceB, ServiceBImpl>();

        // Act
        var serviceB = _provider.Resolve<IServiceB>();

        // Assert
        Assert.NotNull(serviceB);
        Assert.IsType<ServiceBImpl>(serviceB);
    }
    #endregion
}

#region Test Interfaces and Classes
public interface IService;

public class ServiceImpl : IService;

public interface IServiceA;

public class ServiceAImpl(IService service) : IServiceA;

public interface IServiceB;

public class ServiceBImpl : IServiceB
{
    public ServiceBImpl(ISvcContainer container, ISvcProvider provider)
    {
        _ = provider.Resolve<IServiceA>();
        container.Register<IServiceC, ServiceCImpl>(SvcLifetime.Transient);
        _ = provider.Resolve<IServiceC>();
    }
}

public interface IServiceC;

public class ServiceCImpl : IServiceC;

public interface INode1;

public interface INode2;

public interface INode3;

public class Node1(INode3 _) : INode1;

public class Node2(INode1 _) : INode2;

public class Node3(INode2 _) : INode3;

public interface INonExistingService;
#endregion
