using System.Net;

namespace OBP.Core.HttpClient
{
	/// <summary>
	/// Ответ на запрос
	/// </summary>
	/// <typeparam name="T">Тип ожидаемого ответа</typeparam>
	public class ResultMessage<T>
	{
		/// <summary>
		/// Статус ответа
		/// </summary>
		public HttpStatusCode StatusCode { get; set; }
		
		/// <summary>
		/// Результат успешного запроса. Null, если ошибка
		/// </summary>
		public T Content { get; set; }
		
		/// <summary>
		/// Null, если запрос вернулся без ошибок
		/// </summary>
		public string Error { get; set; }
	}
}