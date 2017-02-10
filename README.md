# TinyCert API Client Library for C\# #

## Description ##

This is the official C\# client library for the [TinyCert](https://www.tinycert.org/) [API](https://www.tinycert.org/docs/api).

## Requirements ##

This project requires a C\# compiler with the .NET Framework version 4.0 or greater.

## Classes ##

The TinyCert namespace provides of 9 publicly accessible classes. They are:

- `Constants` which provides constant values for use in calling various API methods
- `Session` which opens a session on the API server and takes care of carrying out the API requests
- `CA` which allows for retrieving a list of CAs, creating new CAs, retrieving CA certificates, or deleting CAs. The `CA` class must be instantiated with a valid `Session`
- `Certificate` which allows for retrieving lists of certificates, creating new certificates, retrieving existing certificates, changing their status or reissuing them. The `Certificate` class must be instantiated with a valid `Session`
- `CAListItem` which holds a CA's ID and its name. An array of these will be returned by a call to `CA::List()`
- `CertListItem` which holds a certificate's ID and its common name. An array of these will be returned by a call to `Certificate::List(int)`
- `ApiException` which is thrown on error. Check the API documentation for full details on which errors may be encountered  

## Example usage ##

The following are some brief examples on the various things you can do with this client library.

### Sample 1: Retrieving details on every CA in the account ###

```csharp
using System;
using TinyCert;

public class TCDemo
{
	public static void Main ()
	{
		try
		{
			//Initiate a connection
			TinyCert.Session tc = new TinyCert.Session ("MySuperSecretApiKey");
			tc.Connect ("account@example.com", "My Secret Passphrase");
			
			//Iterate over all CA's in the account
			TinyCert.CA tcca = new TinyCert.CA (tc);
			foreach (TinyCert.CAListItem ca_item in tcca.List ())
			{
				//Request CA details and print on the console
				CADetailsResponse ca = tcca.Details (ca_item.id);
				Console.WriteLine ("Common Name: " + ca.CN);
				Console.WriteLine ("Organisational Unit: " + ca.OU);
				Console.WriteLine ("Organisation: " + ca.O);
				Console.WriteLine ("Locality: " + ca.L);
				Console.WriteLine ("State/Province: " + ca.ST);
				Console.WriteLine ("Country: " + ca.C);
				Console.WriteLine ("Email Address: " + ca.E);
				Console.WriteLine ("Hashing algorithm: " + ca.hash_alg);
				
				//Request CA certificate and print it, too
				Console.WriteLine (tcca.GetCertificate (ca_item.id));
				Console.WriteLine ("");
			}
			
			//Clean up nicely
			tc.Disconnect ();
		} catch (TinyCert.ApiException e)
		{
			Console.Write ("ERROR: " + e.Status + " - " + e.Code + " - " + e.Message);
		}
	}
}
```

### Sample 2: Creating a new CA ###

```csharp
using System;
using TinyCert;

public class TCDemo
{
	public static void Main ()
	{
		try
		{
			//Initiate a connection
			TinyCert.Session tc = new TinyCert.Session ("MySuperSecretApiKey");
			tc.Connect ("account@example.com", "My Secret Passphrase");
			
			//Create a new (French) CA
			TinyCert.CA tcca = new TinyCert.CA (tc);
			int new_ca_id = ca.Create ("My Organisation", "My Locality", "My State", "FR", TinyCert.Constants.HASH_SHA256);
			
			//Clean up nicely
			tc.Disconnect ();
		} catch (TinyCert.ApiException e)
		{
			Console.Write ("ERROR: " + e.Status + " - " + e.Code + " - " + e.Message);
		}
	}
}
```

### Sample 3: Dumping all certificates and their CSRs for a given CA to the console ###

```csharp
using System;
using TinyCert;

public class TCDemo
{
	public static void Main ()
	{
		try
		{
			//Initiate a connection
			TinyCert.Session tc = new TinyCert.Session ("MySuperSecretApiKey");
			tc.Connect ("account@example.com", "My Secret Passphrase");
			
			//Refer to sample 1 to learn how to retrieve an actual Certificate Authority ID
			int ca_id = 561;
			
			//Iterate over all certificates in the selected CA
			TinyCert.Certificate tccert = new TinyCert.Certificate (tc);
			foreach (TinyCert.CertListItem cert_item in tccert.List (ca_id, TinyCert.Constants.STATUS_ALL))
			{
				//Request certificate details and print on the console
				TinyCert.CertDetailsResponse cert = tccert.Details (cert_item.id);
				Console.WriteLine ("CN: " + cert.CN);
				Console.WriteLine ("O: " + cert.O);
				Console.WriteLine ("OU: " + cert.OU);
				Console.WriteLine ("L: " + cert.L);
				Console.WriteLine ("ST: " + cert.ST);
				Console.WriteLine ("C: " + cert.C);
				if (cert.Alt != null)
				{
					Console.Write ("Alt:");
					foreach (TinyCert.SAN alt in cert.Alt)
					{
						if (alt.DNS != null) Console.Write (" DNS:" + alt.DNS);
						if (alt.IP != null) Console.Write (" IP:" + alt.IP);
						if (alt.email != null) Console.Write (" email:" + alt.email);
						if (alt.URI != null) Console.Write (" URI:" + alt.URI);
					}
					Console.WriteLine ("");
				}
				Console.WriteLine (tccert.Get (cert_item.id, TinyCert.Constants.REQUEST));
				Console.WriteLine ("");
			}
			
			//Clean up nicely
			tc.Disconnect ();
		} catch (TinyCert.ApiException e)
		{
			Console.Write ("ERROR: " + e.Status + " - " + e.Code + " - " + e.Message);
		}
	}
}
```

### Sample 4: Manipulating an existing certificate ### 

```csharp
using System;
using TinyCert;

public class TCDemo
{
	public static void Main ()
	{
		try
		{
			//Initiate a connection
			TinyCert.Session tc = new TinyCert.Session ("MySuperSecretApiKey");
			tc.Connect ("account@example.com", "My Secret Passphrase");
			
			//Refer to sample 3 to learn how to retrieve an actual Certificate ID
			int cert_id = 561;
			
			TinyCert.Certificate tccert = new TinyCert.Certificate (tc);
			
			//Place the certificate on hold
			tccert.Status (cert_id, "hold");
			//Restore it to normal
			tccert.Status (cert_id, "good");
			//Even revoke it
			tccert.Status (cert_id, "revoked");
			//Reissue a new certificate
			int new_cert_id = tccert.Reisue (cert_id);
			
			//Clean up nicely
			tc.Disconnect ();
		} catch (TinyCert.ApiException e)
		{
			Console.Write ("ERROR: " + e.Status + " - " + e.Code + " - " + e.Message);
		}
	}
}
```

### Sample 5: Creating a new certificate ###

```csharp
using System;
using TinyCert;

public class TCDemo
{
	public static void Main ()
	{
		try
		{
			//Initiate a connection
			TinyCert.Session tc = new TinyCert.Session ("MySuperSecretApiKey");
			tc.Connect ("account@example.com", "My Secret Passphrase");
			
			//Refer to sample 1 to learn how to retrieve an actual Certificate Authority ID
			int ca_id = 561;
			
			TinyCert.Certificate tccert = new TinyCert.Certificate (tc);
			
			//Create a simple certificate for an email address
			int email_cert_id = tccert.Create (ca_id, "user@example.com", null, "Testing Department", null, null, "GB", null);
			
			//Create a certificate for a web site, allowing both the bare domain and the www subdomain
			SAN[] SANs = new SAN [2];
			SANs [0] = new SAN (); SANs [0].DNS = "www.example.com";
			SANs [1] = new SAN (); SANs [1].URI = "example.com";
			int web_cert_id = tccert.Create (ca_id, "example.com", "My Department", "My Organisation", "My Town", "My State", "DE", SANs);
			
			//Create a more extensive certificate with lots of stuff
			SANs = new SAN [4];
			SANs [0] = new SAN (); SANs [0].IP = "127.0.0.1";
			SANs [1] = new SAN (); SANs [1].email = "user@example.com";
			SANs [2] = new SAN (); SANs [2].DNS = "www.example.com";
			SANs [3] = new SAN (); SANs [3].URI = "http://example.org/";
			int full_cert_id = tccert.Create (ca_id, "My Common Name", "My Department", "My Organisation", "My Town", "My State", "US", SANs);
			
			//Clean up nicely
			tc.Disconnect ();
		} catch (TinyCert.ApiException e)
		{
			Console.Write ("ERROR: " + e.Status + " - " + e.Code + " - " + e.Message);
		}
	}
}
```

## Copyright and License ##

This software is Copyright (c) 2017 by Steven Don / TinyCert.org

This is free software, licensed under the Simplified BSD license.
