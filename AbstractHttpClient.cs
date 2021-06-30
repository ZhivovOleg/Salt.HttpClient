using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace OBP.Core.HttpClient
{
	/// <summary>
	/// Родительский класс для кастомных типизированных http-классов, работающих с IHttpClientFactory
	/// </summary>
	public abstract class AbstractHttpClient
	{
		#region DI

		private readonly ILogger _logger;
		private readonly System.Net.Http.HttpClient _httpClient;

		/// <summary>
		/// Put there logger and httpClient from DI 
		/// </summary>
		protected AbstractHttpClient(System.Net.Http.HttpClient httpClient, ILogger logger)
		{
			_httpClient = httpClient;
			_logger = logger;
		}

		#endregion

		#region Private
		
		private async Task<ResultMessage<T>> PrepareResultMessage<T>(HttpResponseMessage result)
		{
			ResultMessage<T> resultMessage = new() { StatusCode = result.StatusCode };
			string resultString = await result.Content.ReadAsStringAsync();

			if (string.IsNullOrEmpty(resultString))
				return resultMessage;
			
			if (result.IsSuccessStatusCode)
				resultMessage.Content = JsonConvert.DeserializeObject<T>(resultString);
			else
				resultMessage.Error = resultString;

			return resultMessage;
		}
		
		private async Task<ResultMessage<T>> Post<T>(string url, Dictionary<string, string> @params = null)
		{
			using (HttpContent content = new StringContent(JsonConvert.SerializeObject(@params)))
			using (HttpResponseMessage response = await _httpClient.PostAsync(url, content))
				return await PrepareResultMessage<T>(response);
		}
		
		private async Task<ResultMessage<T>> Get<T>(string url, Dictionary<string, string> @params = null)
		{
			UriBuilder uriBuilder = new(url);
			if (@params is {Count: > 0})
			{
				NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);
				foreach (KeyValuePair<string, string> parameter in @params)
					query[parameter.Key] = parameter.Value;
				uriBuilder.Query = query.ToString();
			}
			using (HttpResponseMessage response = await _httpClient.GetAsync(uriBuilder.ToString()))
				return await PrepareResultMessage<T>(response);
		}

		private async Task<ResultMessage<T>> AsyncOperation<T>(HttpMethod method, string actionPath, Dictionary<string, string> @params = null)
		{
			try
			{
				UriBuilder uriBuilder = new(_httpClient.BaseAddress) { Path = actionPath };
				return method switch
				{
					{ } m when m == HttpMethod.Get => await Get<T>(uriBuilder.ToString(), @params),
					{ } m when m == HttpMethod.Post => await Post<T>(uriBuilder.ToString(), @params),
					_ => throw new Exception($"Метод {method.Method} не реализован")
				};
			}
			catch (Exception exc)
			{
				_logger.LogError($"Error on {method.Method} from '{_httpClient.BaseAddress}/{actionPath}' : {exc.Message}", exc);
				throw;
			}
		}

		private ResultMessage<T> SyncOperation<T>(HttpMethod method, string actionPath, Dictionary<string, string> @params = null)
		{
			try
			{
				return AsyncOperation<T>(method, actionPath, @params).ConfigureAwait(false).GetAwaiter().GetResult();
			}
			catch (ObjectDisposedException odexc)
			{
				_logger.LogError($"Error on sync {method.Method} from '{_httpClient.BaseAddress}/{actionPath}' : {odexc.Message}", odexc);
				throw;
			}
			catch (InvalidOperationException ioexc)
			{
				_logger.LogError($"Error on sync {method.Method} from '{_httpClient.BaseAddress}/{actionPath}' : {ioexc.Message}", ioexc);
				throw;
			}
		}
	
		#endregion Private

		/// <summary>
		/// Отправка синхронного запроса GET
		/// </summary>
		/// <param name="actionPath">путь к Action</param>
		/// <param name="params">список параметров</param>
		/// <typeparam name="T">Тип ответа</typeparam>
		/// <returns>OBP.Core.HttpClient.ResultMessage</returns>
		protected ResultMessage<T> SyncGet<T>(string actionPath, Dictionary<string, string> @params = null) =>
			SyncOperation<T>(HttpMethod.Get, actionPath, @params);

		/// <summary>
		/// Отправка синхронного запроса POST
		/// </summary>
		/// <param name="actionPath">путь к Action</param>
		/// <param name="params">список параметров</param>
		/// <typeparam name="T">Тип ответа</typeparam>
		/// <returns>OBP.Core.HttpClient.ResultMessage</returns>
		protected ResultMessage<T> SyncPost<T>(string actionPath, Dictionary<string, string> @params = null) =>
			SyncOperation<T>(HttpMethod.Post, actionPath, @params);
		
		/// <summary>
		/// Отправка асинхронного запроса GET
		/// </summary>
		/// <param name="actionPath">путь к Action</param>
		/// <param name="params">список параметров</param>
		/// <typeparam name="T">Тип ответа</typeparam>
		/// <returns>OBP.Core.HttpClient.ResultMessage</returns>
		protected async Task<ResultMessage<T>> AsyncGet<T>(string actionPath, Dictionary<string, string> @params = null) =>
			await AsyncOperation<T>(HttpMethod.Get, actionPath, @params);
	
		/// <summary>
		/// Отправка синхронного запроса POST
		/// </summary>
		/// <param name="actionPath">путь к Action</param>
		/// <param name="params">список параметров</param>
		/// <typeparam name="T">Тип ответа</typeparam>
		/// <returns>OBP.Core.HttpClient.ResultMessage</returns>
		protected async Task<ResultMessage<T>> AsyncPost<T>(string actionPath, Dictionary<string, string> @params = null) =>
			await AsyncOperation<T>(HttpMethod.Post, actionPath, @params);
	}
}

