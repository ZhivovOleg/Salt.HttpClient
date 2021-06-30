# Salt.HttpClient
Typed abstract httpClient for IHttpClientFactory.
Contains Sync/Async methods for creating requests.
Also contains basic 

# Using 
by concept https://docs.microsoft.com/ru-ru/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests

1. Create your own typed httpClient
    ```c#
    public class TestClient : AbstractHttpClient
    {
        public TestClient(HttpClient httpClient, ILogger<TestClient> logger) : base(httpClient, logger)
        { }

        public ResultMessage<Answer[]> Get()
        {
            return SyncGet<Answer[]>("Catalogs/users", new Dictionary<string, string>(){ {"name", "Вася"} });
        }

        public async Task<ResultMessage<Answer[]>> GetAsync()
        {
            return await AsyncGet<Answer[]>("Catalogs/users", new Dictionary<string, string>(){ {"name", "Вася"} });
        }
    }
    ```

3. In ```Startup.cs``` in method ```void AddServices(IServiceCollection services)``` add your client to serviceCollection Factory:
    ```c#
    services.AddSaltHttpClient<TestClient>(baseUrl);
    ```
