using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace OBP.Core.HttpClient
{
	/// <summary>
	/// Расширения для инжекта ObpHttpClient'ов в IServiceCollection
	/// </summary>
	public static class ServiceCollectionExtension
	{
		private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int times) =>
			HttpPolicyExtensions
				.HandleTransientHttpError()
				.OrResult(msg => msg.StatusCode is HttpStatusCode.NotFound)
				.WaitAndRetryAsync(times, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

		/// <summary>
		/// Добавление в DI типизированного httpClient
		/// </summary>
		/// <param name="serviceCollection">IServiceCollection</param>
		/// <param name="baseUrl">базовый Url конечного узла</param>
		/// <param name="timeout">Таймаут запроса</param>
		/// <param name="baseHeaders">Базовые headers для каждого запроса</param>
		/// <param name="retryNumber">Количество попыток подключения, если в качестве ответов падают ошибки 5ХХ, 408 (timeout) или 404 (notFound)</param>
		/// <typeparam name="TRealization">Тип, наследующися от AbstractHttpClient</typeparam>
		/// <returns></returns>
		public static IServiceCollection AddObpHttpClient<TRealization>(this IServiceCollection serviceCollection
			, Uri baseUrl
			, TimeSpan? timeout = null
			, HttpHeaders baseHeaders = null
			, int retryNumber = 0)
			where TRealization : AbstractHttpClient
		{
			IHttpClientBuilder httpClientBuilder = serviceCollection.AddHttpClient<TRealization>(httpClient =>
				{
					httpClient.BaseAddress = baseUrl;
					if (timeout.HasValue)
						httpClient.Timeout = timeout.Value;
					if (baseHeaders != null && baseHeaders.Any())
						foreach (KeyValuePair<string, IEnumerable<string>> header in baseHeaders)
							httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
				});
			httpClientBuilder.SetHandlerLifetime(TimeSpan.FromMinutes(5));
			
			if(retryNumber > 0)
				httpClientBuilder.AddPolicyHandler(GetRetryPolicy(retryNumber));
			
			return serviceCollection;
		}
	}
}