using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Salt.HttpClient
{
	/// <summary>
	/// Родительский класс для кастомных типизированных http-классов, работающих с IHttpClientFactory
	/// </summary>
	public abstract class AbstractHttpClient
	{
		#region DI

		protected ILogger Logger { get; }
		private readonly System.Net.Http.HttpClient _httpClient;

		/// <summary>
		/// Put there logger and httpClient from DI 
		/// </summary>
		protected AbstractHttpClient(System.Net.Http.HttpClient httpClient, ILogger logger)
		{
			_httpClient = httpClient;
			Logger = logger;
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
			using (HttpResponseMessage response = await _httpClient.GetAsync(GetUriString(url, @params)))
				return await PrepareResultMessage<T>(response);
		}

		private async Task<ResultMessage<T>> Send<T>(
			string actionPath,
			HttpMethod httpMethod,
			Dictionary<string, string> @params = null,
			Dictionary<string, string> cookies = null)
		{
			try
			{
				Uri uri = new Uri(_httpClient.BaseAddress, actionPath);

				using (HttpRequestMessage requestMessage =
					GetHttpRequestMessage(uri.ToString(), httpMethod, @params, cookies))
				using (HttpResponseMessage response = await _httpClient.SendAsync(requestMessage))
					return await PrepareResultMessage<T>(response);
			}
			catch (Exception exc)
			{
				Logger.LogError($"Error on {httpMethod.Method} from '{_httpClient.BaseAddress}/{actionPath}' : {exc.Message}", exc);
				throw;
			}
		}

		private HttpRequestMessage GetHttpRequestMessage(
			string url,
			HttpMethod httpMethod,
			Dictionary<string, string> @params = null,
			Dictionary<string, string> cookies = null)
		{
			HttpRequestMessage requestMessage;

			if (@params == null)
			{
				requestMessage = new HttpRequestMessage(httpMethod, url);
			}
			else if (httpMethod == HttpMethod.Get)
			{
				requestMessage = new HttpRequestMessage(httpMethod, GetUriString(url, @params));
			}
			else
			{
				requestMessage = new HttpRequestMessage(httpMethod, url);
				requestMessage.Content = new StringContent(JsonConvert.SerializeObject(@params));
			}

			if (cookies != null)
			{
				requestMessage.Headers.Add(
					"Cookie",
					string.Join(";", cookies.Select(x => $"{x.Key}={x.Value}")));
			}

			return requestMessage;
		}
		
		private string GetUriString(string url, Dictionary<string, string> @params = null)
		{
			UriBuilder uriBuilder = new(url);
			if (@params is {Count: > 0})
			{
				NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);
				foreach (KeyValuePair<string, string> parameter in @params)
					query[parameter.Key] = parameter.Value;
				uriBuilder.Query = query.ToString();
			}

			return uriBuilder.ToString();
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
				Logger.LogError($"Error on {method.Method} from '{_httpClient.BaseAddress}/{actionPath}' : {exc.Message}", exc);
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
				Logger.LogError($"Error on sync {method.Method} from '{_httpClient.BaseAddress}/{actionPath}' : {odexc.Message}", odexc);
				throw;
			}
			catch (InvalidOperationException ioexc)
			{
				Logger.LogError($"Error on sync {method.Method} from '{_httpClient.BaseAddress}/{actionPath}' : {ioexc.Message}", ioexc);
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
		
		/// <summary>
		/// ОТправка асинхронного запроса с возможностью указывать куки.
		/// </summary>
		/// <param name="actionPath">путь к Action</param>
		/// <param name="method">тип запроса (Get, Post, ...)</param>
		/// <param name="params">список параметров</param>
		/// <param name="cookies">Куки для отправки</param>
		/// <typeparam name="T">Тип ответа</typeparam>
		/// <returns>OBP.Core.HttpClient.ResultMessage</returns>
		protected async Task<ResultMessage<T>> AsyncSend<T>(
			string actionPath,
			HttpMethod method,
			Dictionary<string, string> @params = null,
			Dictionary<string, string> cookies = null) =>
			await Send<T>(actionPath, method, @params, cookies);
	}
}

