using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace TinyCert
{
	/* Constants */
	
	public static class Constants
	{
		public const string CERTIFICATE = "cert";
		public const string CERTIFICATE_CHAIN = "chain";
		public const string HASH_SHA1 = "sha1";
		public const string HASH_SHA256 = "sha256";
		public const string PKCS12 = "pkcs12";
		public const string PRIVKEY_DECRYPTED = "key.dec";
		public const string PRIVKEY_ENCRYPTED = "key.enc";
		public const string REQUEST = "csr";
		public const int STATUS_EXPIRED = 1;
		public const int STATUS_GOOD = 2;
		public const int STATUS_REVOKED = 4;
		public const int STATUS_HOLD = 8;
		public const int STATUS_ALL = 15;
	}
	
	/* API responses that form an array */
	
	[DataContract]
	public class ItemResponse {}
	
	[DataContract]
	public class CAListItem : ItemResponse
	{
		[DataMember] public int id { get; set; }
		[DataMember] public string name { get; set; }
	}
	
	[DataContract]
	public class CertListItem : ItemResponse
	{
		[DataMember] public int id { get; set; }
		[DataMember] public string name { get; set; }
		[DataMember] public string status { get; set; }
		[DataMember] public int expires { get; set; }
	}
	
	/* API responses that form a single object */
	
	[DataContract]
	public class ObjectResponse {}
	
	[DataContract]
	internal class ErrorResponse : ObjectResponse
	{
		[DataMember] public string code { get; set; }
		[DataMember] public string text { get; set; }
	}
	
	[DataContract]
	public class CADetailsReponse : ObjectResponse
	{
		[DataMember] public int id { get; set; }
		[DataMember] public string C { get; set; }
		[DataMember] public string ST { get; set; }
		[DataMember] public string L { get; set; }
		[DataMember] public string O { get; set; }
		[DataMember] public string OU { get; set; }
		[DataMember] public string CN { get; set; }
		[DataMember] public string E { get; set; }
		[DataMember] public string hash_alg { get; set; }
	}
	
	[DataContract]
	internal class CAIDResponse : ObjectResponse
	{
		[DataMember] public int ca_id { get; set; }
	}
	
	[DataContract]
	public class CertDetailsResponse : ObjectResponse
	{
		[DataMember] public int id { get; set; }
		[DataMember] public string status { get; set; }
		[DataMember] public string C { get; set; }
		[DataMember] public string ST { get; set; }
		[DataMember] public string L { get; set; }
		[DataMember] public string O { get; set; }
		[DataMember] public string OU { get; set; }
		[DataMember] public string CN { get; set; }
		[DataMember] public SAN[] Alt { get; set; }
	}
	
	[DataContract]
	internal class CertIDResponse : ObjectResponse
	{
		[DataMember] public int cert_id { get; set; }
	}
	
	[DataContract]
	internal class ConnectResponse : ObjectResponse
	{
		[DataMember] public string token { get; set; }
	}
	
	[DataContract]
	internal class GetResponse : ObjectResponse
	{
		[DataMember] public string pem { get; set; }
		[DataMember] public string pkcs12 { get; set; }
	}
	
	[DataContract]
	public class SAN
	{
		[DataMember] public string DNS { get; set; }
		[DataMember] public string email { get; set; }
		[DataMember] public string IP { get; set; }
		[DataMember] public string URI { get; set; }
	}
	
	public class ApiException : Exception
	{
		public readonly int Status;
		public readonly string Code;
		public ApiException (string text) : base (text)
		{
			this.Status = -1;
			this.Code = "UnknownError";
		}
		public ApiException (int status, string code, string text) : base (text)
		{
			this.Status = status;
			this.Code = code;
		}
	}
	
	public class Session
	{
		private HMACSHA256 hmac;
		internal string sessionToken;
		
		public Session (string apiKey)
		{
			this.hmac = new HMACSHA256 (System.Text.Encoding.UTF8.GetBytes (apiKey));
		}
		
		private HttpWebResponse DoRequest (string endpoint, SortedDictionary<string, string> args)
		{
			//URL is easily constructed
			string requestURL = "https://www.tinycert.org/api/v1/" + endpoint;
			
			//Build request body
			byte[] bodyBytes;
			int index = 0;
			System.Text.StringBuilder sbBody = new System.Text.StringBuilder ();
			foreach (KeyValuePair<string, string> kvp in args)
			{
				if (kvp.Value == null) continue;
				if (index++ > 0) sbBody.Append ("&");
				sbBody.Append (System.Net.WebUtility.UrlEncode (kvp.Key));
				sbBody.Append ("=");
				sbBody.Append (System.Net.WebUtility.UrlEncode (kvp.Value));
			}
			bodyBytes = System.Text.Encoding.UTF8.GetBytes (sbBody.ToString ());
			
			//Calculate hash and append it
			byte[] hashBytes = this.hmac.ComputeHash (bodyBytes);
			sbBody.Append ("&digest=");
			foreach (byte b in hashBytes) sbBody.Append (b.ToString ("x2"));
			bodyBytes = System.Text.Encoding.UTF8.GetBytes (sbBody.ToString ());
			
			//Send out request
			WebRequest request = WebRequest.Create (requestURL);
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";
			request.ContentLength = bodyBytes.Length;
			Stream dataStream = request.GetRequestStream ();
			dataStream.Write (bodyBytes, 0, bodyBytes.Length);
			dataStream.Close ();
			
			//Get the response
			HttpWebResponse response;
			try
			{
				return request.GetResponse () as HttpWebResponse;
			} catch (WebException e)
			{
				if (e.Response == null) throw new ApiException ("No response from the API server");
				
				response = e.Response as HttpWebResponse;
				DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer (typeof (ErrorResponse));
				ErrorResponse errorResponse = jsonSerializer.ReadObject (response.GetResponseStream ()) as ErrorResponse;
				throw new ApiException ((int)response.StatusCode, errorResponse.code, errorResponse.text);
			}
		}
		
		internal ObjectResponse RequestObject (Type responseType, string endpoint, SortedDictionary<string, string> args)
		{
			HttpWebResponse response = DoRequest (endpoint, args);
			DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer (responseType);
			return jsonSerializer.ReadObject (response.GetResponseStream ()) as ObjectResponse;
		}
		
		internal ItemResponse[] RequestArray (Type responseType, string endpoint, SortedDictionary<string, string> args)
		{
			HttpWebResponse response = DoRequest (endpoint, args);
			DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer (responseType);
			return jsonSerializer.ReadObject (response.GetResponseStream ()) as ItemResponse[];
		}
		
		public void Connect (string email, string passPhrase)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("email", email);
			args.Add ("passphrase", passPhrase);
			ConnectResponse resp = this.RequestObject (typeof (ConnectResponse), "/connect", args) as ConnectResponse;
			this.sessionToken = resp.token;
		}
		
		public void Disconnect ()
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("token", this.sessionToken);
			this.RequestObject (typeof (ObjectResponse), "/disconnect", args);
		}
	}
	
	public class CA
	{
		private Session session;
		
		public CA (Session session)
		{
			this.session = session;
		}
		
		public int Create (string O, string L, string ST, string C, string hash_method)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("C", C);
			args.Add ("L", L);
			args.Add ("O", O);
			args.Add ("ST", ST);
			args.Add ("hash_method", hash_method);
			args.Add ("token", this.session.sessionToken);
			return (this.session.RequestObject (typeof (CAIDResponse), "/ca/new", args) as CAIDResponse).ca_id;
		}
		
		public void Delete (int ca_id)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("ca_id", ca_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			this.session.RequestObject (typeof (ObjectResponse), "/ca/details", args);
		}
		
		public CADetailsReponse Details (int ca_id)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("ca_id", ca_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			return this.session.RequestObject (typeof (CADetailsReponse), "/ca/details", args) as CADetailsReponse;
		}
		
		public string GetCertificate (int ca_id)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("ca_id", ca_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			args.Add ("what", Constants.CERTIFICATE);
			return (this.session.RequestObject (typeof (GetResponse), "/ca/get", args) as GetResponse).pem;
		}
		
		public CAListItem[] List ()
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("token", this.session.sessionToken);
			return this.session.RequestArray (typeof (CAListItem[]), "/ca/list", args) as CAListItem[];
		}
	}
	
	public class Certificate
	{
		private Session session;
		
		public Certificate (Session session)
		{
			this.session = session;
		}
		
		public int Create (int ca_id, string CN, string OU, string O, string L, string ST, string C, SAN[] Alt)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("C", C);
			args.Add ("CN", CN);
			args.Add ("L", L);
			args.Add ("O", O);
			args.Add ("OU", OU);
			if (Alt != null && Alt.Length > 0)
			{
				int index = 0;
				foreach (SAN san in Alt)
				{
					string prefix = "SANs[" + (index++) + "]";
					if (san.DNS != null) args.Add (prefix + "[DNS]", san.DNS);
					if (san.email != null) args.Add (prefix + "[email]", san.email);
					if (san.IP != null) args.Add (prefix + "[IP]", san.IP);
					if (san.URI != null) args.Add (prefix + "[URI]", san.URI);
				}
			}
			args.Add ("ST", ST);
			args.Add ("ca_id", ca_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			return (this.session.RequestObject (typeof (CertIDResponse), "/cert/new", args) as CertIDResponse).cert_id;
		}
		
		public CertDetailsResponse Details (int cert_id)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("cert_id", cert_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			return this.session.RequestObject (typeof (CertDetailsResponse), "/cert/details", args) as CertDetailsResponse;
		}
		
		public string Get (int cert_id, string what)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("cert_id", cert_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			args.Add ("what", what);
			
			GetResponse resp = this.session.RequestObject (typeof (GetResponse), "/cert/get", args) as GetResponse;
			return (resp.pem == null) ? resp.pkcs12 : resp.pem;
		}
		
		public CertListItem[] List (int ca_id, int what)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("ca_id", ca_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			args.Add ("what", what.ToString ());
			return this.session.RequestArray (typeof (CertListItem[]), "/cert/list", args) as CertListItem[];
		}
		
		public int Reissue (int cert_id)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("cert_id", cert_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			return (this.session.RequestObject (typeof (CertIDResponse), "/cert/details", args) as CertIDResponse).cert_id;
		}
		
		public void Status (int cert_id, string status)
		{
			SortedDictionary<string, string> args = new SortedDictionary<string, string> (StringComparer.Ordinal);
			args.Add ("cert_id", cert_id.ToString ());
			args.Add ("token", this.session.sessionToken);
			args.Add ("status", status);
			this.session.RequestObject (typeof (ObjectResponse), "/cert/status", args);
		}
	}
}
