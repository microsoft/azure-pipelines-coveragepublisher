namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public class ServiceFactory
    {
        public virtual BuildService GetBuildService(IClientFactory clientFactory, IPipelinesExecutionContext executionContext)
        {
            return new BuildService(clientFactory, executionContext.ProjectId);
        }

        public virtual FileContainerService GetFileContainerService(IClientFactory clientFactory, IPipelinesExecutionContext executionContext)
        {
            return new FileContainerService(clientFactory, executionContext);
        }
    }
}
