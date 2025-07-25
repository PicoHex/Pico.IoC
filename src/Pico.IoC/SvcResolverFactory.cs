namespace Pico.IoC;

public class SvcResolverFactory : ISvcResolverFactory
{
    public ISvcResolver CreateResolver(ISvcContainer container, ISvcProvider provider) =>
        new SvcResolver(container, provider);
}
