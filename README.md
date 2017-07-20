# NuSearch

A tutorial repository for Elasticsearch and NEST.

Simply clone the repository and follow the README.md to build out a feature-packed search interface for NuGet packages!

## NOTE !

If you are reading this on a fork, note that it may be out of sync with: https://github.com/elastic/elasticsearch-net-example

The solution works in Visual Studio, Visual Studio for Mac, Visual Studio Code and JetBrains Rider, so feel free to play around with any of these.

Oh, and if you find any bugs along the way, send us a PR! Or feel free to ask questions on this repositories github issues.

# Part 1: Installing and getting up and running

 - Install latest [Elasticsearch 5.0](https://www.elastic.co/downloads/)

 - Install latest [kibana 5.0](https://www.elastic.co/downloads/kibana)

 - Install [.NET Core SDK 1.0.4 64-Bit for your platform](https://github.com/dotnet/core/blob/master/release-notes/download-archives/1.1.2-download.md)

 - Make sure you have a recent [version of JAVA installed](http://www.oracle.com/technetwork/java/javase/downloads/index.html) (64-Bit JDK recommended)

 - Ensure `JAVA_HOME` environment variable points to the correct JAVA directory e.g. `C:\Program Files\Java\jdk1.8.0_131`

 - Download and extract NuGet feed data. You can obtain a [data dump of nuget here in zip format](https://nusearch.blob.core.windows.net/dump/nuget-data-dec-2016.zip)
 
 - The application we're building is set up to [run traffic through Fiddler](https://www.telerik.com/download/fiddler) if it notices it's running, 
 which is super helpful to see the requests and responses to and from Elasticsearch.

Clone this repository and open the solution using any of

- [Visual Studio](https://www.visualstudio.com/downloads/)
- [Visual Studio for Mac](https://www.visualstudio.com/vs/visual-studio-mac/) 
- [Visual Studio Code](https://code.visualstudio.com/) 
- [JetBrains Rider](https://www.jetbrains.com/rider/)

Once everything is installed, it's time to

- [Start Elasticsearch](https://www.elastic.co/guide/en/elasticsearch/reference/current/install-elasticsearch.html)

- [Start Kibana](https://www.elastic.co/guide/en/kibana/current/install.html)

## Solution structure

The solution consists of 4 projects

```
NuSearch Solution
│
├─ NuSearch.Domain
│  - Shared domain classes
│  
├─ NuSearch.Harvester
│  - A quick and dirty NuGet feed harvester.
│  - No need to run this during the tutorial but if you want newer data than our nuget zip data, it might be worth a run.
│  
├─ NuSearch.Indexer
│  - Takes the nuget xml files and indexes them into Elasticsearch
│  
└─ NuSearch.Web
   - The ASP.NET Core web application we'll be building out
```

The goal of this tutorial is to build these out into a full fledged NuGet package search website.

The projects should in compilable state upon checking out.

# Part 2: Indexing

### Naive indexing approach and some NEST basics

Before we can begin searching for packages we need to index them into Elasticsearch.

Let's take a look at `NuSearch.Indexer` project first. This is the console application that will be responsible for reading in the package data from the NuGet feed files and indexing them into ES.

In `Main()` we have the following code:

```csharp
static void Main(string[] args)
{
	Client = NuSearchConfiguration.GetClient();
	string directory = args.Length > 0 && !string.IsNullOrEmpty(args[0]) 
		? args[0] 
		: NuSearchConfiguration.PackagePath;
	DumpReader = new NugetDumpReader(directory);

	IndexDumps();

	Console.WriteLine("Press any key to continue");
	Console.Read();
}
```

Make sure you change the value assigned to `NuSearchConfiguration.PackagePath` to where you extracted the [nuget data dump](http://esdotnet.blob.core.windows.net/nusearch/nuget-data.zip), or alternatively, pass it in as a command line argument.

Before we start, there are a few classes in the above snippet that we need to gain an understanding of. 

- `ElasticClient` is the entry point into NEST and is the class that is responsible for making requests to all of the Elasticsearch APIs such as indexing and searching, as well as administrative APIs like snapshot/restore, cluster and node stats, etc.  `ElasticClient` is thread-safe; you can create a singleton instance and keep it around in your application, or you can instantiate a new instance for every request.

- `NuSearchConfiguration` is a class specific to this workshop, and holds all of the application specific configuration for our `NuSearch` Elasticsearch cluster.  This centralized configuration is handy since later we'll be building out our web application, so we'll want to use the same settings to talk to Elasticsearch.

Let's take a closer look at `NuSearchConfiguration.GetClient()` method:

```csharp
static NuSearchConfiguration()
{
	_connectionSettings = new ConnectionSettings(CreateUri(9200));
}

public static ElasticClient GetClient() => new ElasticClient(_connectionSettings);
```

We're not doing anything special here (yet).  Just simply returning a new instance of `ElasticClient` that takes a `ConnectionSettings` object with all of the default values.

**NOTE** Because the client is thread-safe, it can live as a singleton within your application. Most of the caches that the NEST creates internally are bound to the `ConnectionSettings` instance, so at the very least, be sure to reuse instances of that. If you are using a Dependency Injection (DI) container, make sure `ConnectionSettings` is registered as singleton so that instances of client share the same cache.

Lastly, `NugetDumpReader` is another workshop specific class that we put together for your convenience to read each the NuGet feed file into a `NugetDump` POCO. `NugetDump` contains a single `IEnumerable<FeedPackage>` property `Dumps`, which holds a collection of packages, each represented as a `FeedPackage`, an unmodified representation of a package mapped from the properties available in the NuGet API.  We create a new instance of `NugetDumpReader` and tell it where the nuget dump files live by passing the path to the files in the constructor.

Now that we're all acquainted with the various classes in our indexer, let's get to the interesting bits and start getting data into Elasticsearch!

First, we'll take the simplest approach and just iterate over each `FeedPackage` in a single dump file and index them individually into an index that we've named `nusearch`.  We can do this by using the `ElasticClient.Index()` method. Put the following into the `IndexDumps()` method

```csharp
static void IndexDumps()
{
	var packages = DumpReader.Dumps.Take(1).First().NugetPackages;
	Console.Write("Indexing documents into elasticsearch...");
	foreach (var package in packages)
	{
		var result = Client.Index(package);

		if (!result.IsValid)
		{
			Console.WriteLine(result.DebugInformation);
			Console.Read();
			Environment.Exit(1);
		}
	}

	Console.WriteLine("Done.");
}
```

*NOTE: We did a `.Take(1)` on the dump files, meaning we are only reading a single file, simply to save us time by not indexing everything right now. In the following sections, we're going to refine our indexing code a few times over.*

The `Index()` method accepts two parameters 

1. the required POCO (document) to index 
2. an optional lambda expression to further define the index request (more on that later).

`Index()` returns an `IIndexResponse` (assigned to `result`) that holds all of the relevant response information returned by Elasticsearch. All response types in NEST including `IIndexResponse` implement `IResponse` which contains a boolean `IsValid` property that tells us whether or not the request was considered valid for the target endpoint.

`IResponse` also contains an `ApiCall` property which holds all of the details about the request such as 

- the original raw JSON that was sent
- the request URL
- the exception from Elasticsearch if the request failed 

as well as other metrics.  We'll touch more on this later in the workshop.

`DebugInformation` is an extremely useful property that describes the state of the request and response, including where it went wrong in the case of errors, as precisely as we can. The information is built lazily when the property is accessed.

Let's run the application using the following command in the terminal, within the `NuSearch.Indexer` directory

```sh
dotnet run
```

and see what we get...

<hr />

You should have gotten an `ElasticsearchClientException`:

```
Dispatching Index() from NEST into to Elasticsearch.NET failed
Received a request marked as PUT
This endpoint accepts POST,PUT
The request might not have enough information provided to make any of these endpoints:
  - /{index=<NULL>}/{type=feedpackage}
  - /{index=<NULL>}/{type=feedpackage}/{id=_Atrico.Lib.CommonAssemblyInfo}
```

_What happened here?_ 

We tried to index a document into Elasticsearch but the client did not have enough information to infer all of the required parts that make up URI _path_ to an Elasticsearch document. 

Both `type` and `id` have a value set but `index` resolved to `NULL`. Why is that?

Notice in our application, we never specified which index to index our packages into, which the index API requires.  
We can specify the index in several ways using NEST, but for now let's just set a default index for the entire client using our `ConnectionSettings`

```csharp
static NuSearchConfiguration()
{
	_connectionSettings = new ConnectionSettings(CreateUri(9200))
		.DefaultIndex("nusearch");
}
```

If we now run our indexer again, after a few seconds, we should see `Done.` written to the console, meaning all of our index requests were successful.

*NOTE* waiting for `Done.` can take a while. We'll explain why it's slow and fix it later!

<hr />

We can check the results of indexing using [Kibana Console](https://www.elastic.co/guide/en/kibana/current/console-kibana.html), by executing a simple match all query against our `nusearch` index (i.e. the SQL equivalent of `SELECT TOP 10 * FROM nusearch` and see what we have

```json
GET /nusearch/_search
{
  "query": {
    "match_all": {}
  }
}
```

You should get back a response similar to the following

```json
{
   "took": 2,
   "timed_out": false,
   "_shards": {
      "total": 5,
      "successful": 5,
      "failed": 0
   },
   "hits": {
      "total": 934,
      "max_score": 1,
      "hits": [
      {
        "_index": "nusearch",
        "_type": "feedpackage",
        "_id": "635200370491429946",
        "_score": 1,
        "_source": {
          "id": "635200370491429946",
          "version": "1.0.0",
          "authors": "bhuvak",
          "created": "2013-11-14T22:44:10.697",
          "dependencies": "",
          "description": "Package description",
          "downloadCount": 126,
          "galleryDetailsUrl": "https://www.nuget.org/packages/635200370491429946/1.0.0",
          "isLatestVersion": false,
          "isAbsoluteLatestVersion": false,
          "isPreRelease": false,
          "lastUpdated": "2015-03-02T12:57:57Z",
          "published": "1900-01-01T00:00:00",
          "packageHash": "xMO6jFY2X6NDhdSKiyZPg5ThiTcJsvu1tEzSokaFydqLaRwZa7mRW8WOKzJn+zJWLJd3Tfm+VyE7iGQXUnVmBw==",
          "packageHashAlgorithm": "SHA512",
          "packageSize": 3908,
          "reportAbuseUrl": "https://www.nuget.org/package/ReportAbuse/635200370491429946/1.0.0",
          "requireLicenseAcceptance": false,
          "tags": " windows8 ",
          "versionDownloadCount": 126,
          "lastEdited": "2013-11-14T22:44:44.063"
        },
      // ... snip for brevity!
    ]
  }
}
```

W00t! We have documents!

A few things to note here by looking at the meta data of one of the returned hits, the objects within the `hits.hits` array; `_index` tells us what index the document belongs to, which in this case is `nusearch` as expected since that's what we set as our default index.  However, the keen observer will have noticed that we never specified a type and yet `_type` here is `feedpackage`. How did that happen?

Since we did not explicitly set a type for the index request, NEST automatically inferred the type `feedpackage` from the C# POCO type name.

Additionally, we did not specify an ID for the document, but notice that `_id` is set to the same value present in our `Id` property (the NuGet package ID).  NEST is smart enough to infer the ID by convention, looking for an `Id` property on the POCO instance.

Let's change the type name here to just `package` instead.  We could do this explicitly in the index request itself:

```csharp
var result = Client.Index(package, i => i
	.Type("package")
);
```

Which could be useful, but we'd have to remember to always specify the type when Elasticsearch requires it.  Most of the time you just want a specific POCO to map to a specific Elasticsearch type, so let's tell NEST to always use the Elasticsearch type `package` when dealing with a `FeedPackage`

```csharp
static NuSearchConfiguration()
{
	_connectionSettings = new ConnectionSettings(CreateUri(9200))
		.SetDefaultIndex("nusearch")
		.InferMappingFor<FeedPackage>(i => i
			.TypeName("package")
		);
}
```

While we're here, let's take this a step further and instead of relying on a default index, let's tell NEST to always route requests to the `nusearch` index when dealing with instances of `FeedPackage`

```csharp
static NuSearchConfiguration()
{
	_connectionSettings = new ConnectionSettings(CreateUri(9200))
		.SetDefaultIndex("nusearch")
		.InferMappingFor<FeedPackage>(i=>i
			.TypeName("package")
			.IndexName("nusearch")
		);
}
```

Now we can re-index our packages using our new settings. First though, we'll need to delete the existing index. Let's add the following to our indexing code to delete the `nusearch` index if it already exists

```csharp
static void DeleteIndexIfExists()
{
	if (Client.IndexExists("nusearch").Exists)
		Client.DeleteIndex("nusearch");
}

static void Main(string[] args)
{
	Client = NuSearchConfiguration.GetClient();
	string directory = args.Length > 0 && !string.IsNullOrEmpty(args[0]) 
		? args[0] 
		: NuSearchConfiguration.PackagePath;
	DumpReader = new NugetDumpReader(directory);

	DeleteIndexIfExists();
	IndexDumps();

	Console.WriteLine("Press any key to continue");
	Console.Read();
}
```

<hr />

Checking our results in Kibana Console again, our hits metadata should now look like this

```json
"_index": "nusearch",
"_type": "package",
"_id": "01d60cc9-6e4d-4119-93d3-9634180b8e87",
"_score": 1,
```

### Bulk indexing

Right now, our indexer iterates over a dump file and indexes each package one at a time.  This means we're making a single HTTP request for **every** package! That may be fine for indexing a few packages here and there, but when we have a large amount of documents, a HTTP request per document is going to add a lot of network overhead.

Instead, we're going to use [Elasticsearch's Bulk API](http://www.elastic.co/guide/en/elasticsearch/reference/current/docs-bulk.html) that will enable us to index multiple documents in a single HTTP request, and in turn give us better performance.

Let's change up our indexing code in `IndexDumps()` to use the `Bulk()` method exposed by NEST:

```csharp
static void IndexDumps()
{
	var packages = DumpReader.Dumps.Take(1).First().NugetPackages;
    Console.Write("Indexing documents into elasticsearch...");
	var result = Client.Bulk(b =>
	{
		foreach(var package in packages)
			b.Index<FeedPackage>(i => i.Document(package));

		return b;
	});

	if (!result.IsValid)
	{
		Console.WriteLine(result.DebugInformation);
		Console.Read();
		Environment.Exit(1);
	}

	Console.WriteLine("Done.");
}
```

<hr />

Here we are using a multi-line lambda expression within the `Bulk()` method call to iterate over our `packages` collection, passing each package to the bulk `Index()` method.  

<hr />

**Note**

Notice here that we had to explicitly state the type of the object we are indexing. Unlike the regular `Client.Index()` method, `Bulk()` can deal with more than one different type in the same request. For example, we could have easily had a `b.Index<Foo>(i => i.Document(myFoo))` in the same `foreach` loop.  In fact, the bulk API supports several operations incuding delete and update in the same bulk call as well.  So we could also have a `b.Delete<FeedPackage>(d => d.Id(123))` in our call too.

<hr />

Let's take a look at the request that `Bulk()` generates.  It is going to build up a JSON payload that the bulk API expects, that is the operation to perform (in our case all `index`) followed by the document to index on the next line:

```json
{ "index" :  {"_index":"nusearch","_type":"package","_id":"NEST"} }
{"id":"NEST","version":"1.4.3.0","authors":"Elasticsearch, Inc." }
{ "index" :  {"_index":"nusearch","_type":"package","_id":"Elasticsearch.Net"} }
{"id":"nest","version":"1.4.3.0","authors":"Elasticsearch, Inc." }
{ "index" :  {"_index":"nusearch","_type":"package","_id":"Newtonsoft.Json"} }
{"id":"Newtonsoft.Json","version":"1.4.3.0","authors":"James K. Newton" }
```

The corresponding response from the bulk API will be a collection of results where each item pertains to the status of the corresponding bulk operation.  For our example above, the response would look like:

```json
"items": [
	{"index":{"_index":"nusearch","_type":"package","_id":"NEST","_version":1,"status":201}},
	{"index":{"_index":"nusearch","_type":"package","_id":"Elasticsearch.NET","_version":1,"status":201}},
	{"index":{"_index":"nusearch","_type":"package","_id":"Newtonsoft.Json","_version":1,"status":201}}
]
```

The `201` status signifying that each bulk operation completed successfully (as per [HTTP Status codes](https://en.wikipedia.org/wiki/List_of_HTTP_status_codes)).

Know that a single failing operation in the bulk call will **not** fail the *entire* request.  For instance, if the `NEST` document above failed
to index for some reason, but the other two documents succeeded, the HTTP status code for the entire request would still be a `200 OK`,
but the corresponding object in the `items` for `NEST` would indicate a failure through the `status` property, likely to be in the `400` to `500` range.

With `NEST`, the `.IsValid` property on the bulk response however will only be `true` if all the individual items succeeded regardless of the actual HTTP status code.

Let's go back to our `IndexDumps()` method and revist our bulk code.

`IBulkResponse` exposes an `ItemsWithErrors` property collection which will contain any bulk operation that failed.

Let's modify `IndexDumps()` again and check the response for errors by adding the following:

```csharp
if (!result.IsValid)
{
	foreach (var item in result.ItemsWithErrors)
		Console.WriteLine("Failed to index document {0}: {1}", item.Id, item.Error);
}
```

Let's go ahead and run our indexer again with

```sh
dotnet run
```

<hr />

All should be well and once again, only `Done.` should be written in the console.  We've just bulk indexed our packages!

### A shortcut to bulk indexing

One of the design goals in NEST is to always maintain a strict 1-to-1 mapping with the Elasticsearch API. Only then do we think about extending the client to expose more convenient ways of accomplishing common uses cases.

For instance, often times the Bulk API is used simply to just index a bunch of documents in one go.  Our current indexing code may seem a bit too verbose for such a simple operation, however NEST doesn't stop you from being explicit with the bulk API if you so desire.

On the other hand, for our case (and also the common case) where we are just indexing documents of the same type, wouldn't it be nice to instead just pass a collection of objects to a method rather than iterating over each one individually?

Well, you can do just that using the `IndexMany()` method, a NEST abstraction over the Bulk API that simply takes a collection of objects and expands to our current bulk indexing implementation.

Let's simplify our code a bit using `IndexMany()` instead:

```csharp
static void IndexDumps()
{
	var packages = DumpReader.Dumps.Take(1).First().NugetPackages;
    Console.Write("Indexing documents into elasticsearch...");
	
	var result = Client.Bulk(b => b.IndexMany(packages));

	if (!result.IsValid)
	{
		foreach (var item in result.ItemsWithErrors)
			Console.WriteLine("Failed to index document {0}: {1}", item.Id, item.Error);

		Console.WriteLine(result.DebugInformation);
		Console.Read();
		Environment.Exit(1);
	}

	Console.WriteLine("Done.");
}
```

Much better, wouldn't you say? :)

### A shortcut to the shortcut

We can shorten this up even further! `.Bulk(b => b.IndexMany(packages))` is useful if we plan on also performing other bulk operations such as updates or deletes in the same request.  However, if we are only interested in indexing, we can simplify this even further by using the `IndexMany()` method exposed directly on `ElasticClient`:


```csharp
static void IndexDumps()
{
	var packages = DumpReader.Dumps.Take(1).First().NugetPackages;
    Console.Write("Indexing documents into elasticsearch...");
	
	var result = Client.IndexMany(packages);

	if (!result.IsValid)
	{
		foreach (var item in result.ItemsWithErrors)
			Console.WriteLine("Failed to index document {0}: {1}", item.Id, item.Error);

		Console.WriteLine(result.DebugInformation);
		Console.Read();
		Environment.Exit(1);
	}

	Console.WriteLine("Done.");
}
```

This is still functionally equivalent to our previous approaches and simply calls into the bulk API, but much more convenient.

### Mappings and document relations

Now that we've bulk indexed our packages, let's take a step back and query for all of our indexes packages using Kibana Console, by issuing a search request to `GET /nusearch/_search`. The results that come back should look similar to

```json
{
   "took": 4,
   "timed_out": false,
   "_shards": {
      "total": 5,
      "successful": 5,
      "failed": 0
  },
   "hits": {
      "total": 934,
      "max_score": 1,
      "hits": [...]
   }
}
```

Something funny is going on here.  If you've noticed, our dump file contained `5000` packages, but our hits total is only `934`.  Where are the missing packages?

The reason for this is due to the fact that our NuGet data contains multiple versions of the same package (i.e. NEST 1.0, NEST 1.1, etc...) and each package version shares the same `Id`.  When Elasticsearch encounters an existing `Id` when indexing, it treats it as an update and overwrites the existing document with the new data.

This is probably useful for many use cases, but for ours, we may want to be able to search for specific versions of a package, or at least view them.  To accomplish this, we are going to have to restructure our models and the way we store them in Elasticsearch by creating a more domain-specific mapping.

As mentioned earlier, our current `FeedPackage` class is an unmodified representation of a package from the NuGet feed.  Instead, let's create our own models that are structured more appropriately for our search application.

Rather than modelling each version of a NuGet package as a separate document, what we want instead is a nested structure, where we have a single top-level package that contains the common/shared information and within it, a collection of the package versions.  We also want to be able to query for a specific version and still return the top-level package. Elasticsearch will gladly deal with complex object graphs, and has a special [nested type](https://www.elastic.co/guide/en/elasticsearch/reference/current/nested.html) specifically for dealing with such relationships.

Before we set this up in Elasticsearch, let's first restructure our `FeedPackage`.

For convenience and for sake of time, this has already been done for you. In our `NuSearch.Domain` project, under the `Model` namespace, there are a few classes that we'll just need to uncomment:

 - `Package.cs`
 - `PackageVersion.cs`
 - `PackageAuthor.cs`
 - `PackageDependency.cs`

`Package` is our top-level representation of a NuGet package and contains all of the common information that is shared across multiple versions of a single package.  Within the `Package` class there is a `PackageVersion` collection, `Versions`, and a `PackageAuthor` collection, `Authors`.

`PackageVersion` contains all of the version specific data for a given package, and within it a `PackageDependency` collection, `Dependencies`.

`PackageAuthor` simply contains a single `Name` property, the publishing author of the package.

And lastly, `PackageDependency` contains dependency information for a given package: the assembly name, version, and supported framework.

Once we've included these new classes in our `NuSearch.Domain` project, the first thing we need to change is the type name and index name mappings on our connection settings to use the new `Package` type (instead of `FeedPackage`):

```csharp
static NuSearchConfiguration()
{
	_connectionSettings = new ConnectionSettings(CreateUri(9200))
		.InferMappingFor<Package>(i => i
			.TypeName("package")
			.IndexName("nusearch")
		);
}
```

Next, we need to create a mapping in Elasticsearch for our `package` type in order to tell Elasticsearch to treat our version, author, and dependency types as nested type.

Prior to this, we haven't set up a mapping.  In fact, we didn't even have to create the index beforehand.  We were able to use the *"schema-less"* nature of Elasticsearch by just indexing documents and letting Elasticsearch create our `nusearch` index on the fly, inferring the types of our properties from the first document of a particular type it sees.  This is known as a [`dynamic mapping`](https://www.elastic.co/guide/en/elasticsearch/reference/current/dynamic-mapping.html) and is very convenient for getting off the ground quickly, but almost never suitable when you would like to search your data in domain specific ways. After all, you know your data better than anyone else!

Let's change up our indexing code a bit and create our index before we actually start indexing.  We can also setup our mapping at the time we create our index.

Let's add a `CreateIndex()` with the following implementation:

```csharp
static void CreateIndex()
{
	Client.CreateIndex("nusearch", i => i
		.Settings(s => s
			.NumberOfShards(2)
			.NumberOfReplicas(0)
		)
		.Mappings(m=>m
			.Map<Package>(map => map
				.AutoMap()
				.Properties(ps => ps
					.Nested<PackageVersion>(n => n
						.Name(p => p.Versions.First())
						.AutoMap()
						.Properties(pps => pps
							.Nested<PackageDependency>(nn => nn
								.Name(pv => pv.Dependencies.First())
								.AutoMap()
							)
						)
					)
					.Nested<PackageAuthor>(n => n
						.Name(p => p.Authors.First())
						.AutoMap()
					)
				)
			)
		)
	);
}
```

So let's break down what we're doing here in the `CreateIndex()` call.  First, we're telling Elasticsearch to create an index named `nusearch` with 2 primary shards and no replicas.  Before this, when we weren't creating the index explicitly and just indexing documents, Elasticsearch automatically created the `nusearch` index with 5 primary shards and 1 replica, the default settings in Elasticsearch.

Next, `Mapping()` allows us to define a mapping for our `Package` POCO.  First we call [`AutoMap()`](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/auto-map.html) which tells NEST to automatically map all of the public properties found in `Package` and figure out their Elasticsearch type based on their C# POCO type.  Also, if we had defined an [`ElasticsearchType` attribute](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/attribute-mapping.html) (something we don't discuss in this workshop) on any of the properties, they would get picked up here.  But instead of using attributes, we are going to define our properties fluently using [`Properties()`](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/fluent-mapping.html).

`Properties()` allows us to override the defaults set by `AutoMap()`.  For instance, our `PackageVersion` collection by default would just be mapped as an `object` type, but instead we want it to be a `nested` type so we call `Nested<T>()`, with `T` being the `PackageVersion` type. We then supply a name, and map its properties as well by making another call to `AutoMap()`.  This process is also repeated for `PackageDependency` which is nested within `PackageVersion`...two levels of nested objects!  And lastly, `PackageAuthor`.

Still with us?  Alright, now that we have our mapping setup, let's reindex our packages.  But wait, `NugetDumpReader.Dumps` just returns a collection of `FeedPackage`s, how do we go from from `FeedPackage` to our new types?  More importantly, as we iterate through the dump files, we need a way of determining when we've encountered a package for the first time in order to treat all subsequent encounters of the same package as just another version that we can include in the "main" package.

Now we just need to make a small change to `IndexDumps()` and have it call `GetPackages()` instead of reading from `Dumps` directly by changing line 1 to:

```c#
var packages = DumpReader.GetPackages();
```

`GetPackages()` creates a dictionary of packages keyed by their ID and iterates through each package in the dump file(s).  When it encounters a new package it simply adds it to the dictionary.  When it encounters a package that already exists in the dictionary, then it simply adds the package to the `Versions` collection of the package that already exists in the dictionary.  At the end, it simply returns all of the packages as a single collection of `Package`s.  Also, take note that all of the property mapping and parsing between `FeedPackage`, `Package`, and `PackageVersion` takes place in the constructors of `Package` and `PackageVersion`.

Also, we don't want to bulk index _all_ of our data in one request, since it's nearly 700mb! Instead, we should limit the amount of data in one bulk request. There's another Bulk method in NEST, `BulkAll()`, that is helpful when we need to partition a collection of documents up into smaller collections and make multiple bulk requests. It uses an
observable design pattern to

1. Set up the conditions for the operation that will be observed
2. Subscribe an observer to the operation to observe when
	1. the next bulk request is issued
	2. if an error occurs within a bulk request
	3. the operation has completed.

Let's wire up `BulkAll()`

```csharp
static void IndexDumps()
{
	Console.WriteLine("Setting up a lazy xml files reader that yields packages...");
	var packages = DumpReader.GetPackages();

	Console.Write("Indexing documents into elasticsearch...");
	var waitHandle = new CountdownEvent(1);

	var bulkAll = Client.BulkAll(packages, b => b
		.BackOffRetries(2)
		.BackOffTime("30s")
		.RefreshOnCompleted(true)
		.MaxDegreeOfParallelism(4)
		.Size(1000)
	);

	bulkAll.Subscribe(new BulkAllObserver(
		onNext: b => Console.Write("."),
		onError: e => throw e,
		onCompleted: () => waitHandle.Signal()
	));

	waitHandle.Wait();
	Console.WriteLine("Done.");
}
```

`GetPackages()` is still a heavy routine as it needs to load all the files data into memory before we can index, but in using `BulkAll()`, issuing bulk requests to index all the packages is made much simpler. In each bulk request, we're indexing 1000 documents using the `Size(1000)` property. How do you know how many documents to index in each request? The magic number is going to depend on the overall size of each bulk request in bytes, the hardware used in the cluster, the configuration of the cluster, complexity of the documents etc. There are no hard and fast rules for what a good number will be and the only true way of knowing is to benchmark it. For our purposes in this workshop however, 1000 documents in each bulk request should work well.

Finally, let's add a call to `CreateIndex()` before `IndexDumps()`.

`Main()` should now look like:

```csharp
static void Main(string[] args)
{
	Client = NuSearchConfiguration.GetClient();
	string directory = args.Length > 0 && !string.IsNullOrEmpty(args[0] 
		? args[0] 
		: NuSearchConfiguration.PackagePath;
	DumpReader = new NugetDumpReader(directory);

    DeleteIndexIfExists();
	CreateIndex();
	IndexDumps();

	Console.WriteLine("Press any key to continue");
	Console.Read();
}
```

Let's run our indexer one more time, this time indexing **everything!**

We now should see heaps more packages in our `nusearch` index:

```json
{
  "took": 74,
  "timed_out": false,
  "_shards": {
    "total": 2,
    "successful": 2,
    "failed": 0
  },
  "hits": {
    "total": 40740,
```

That's better!

### Aliases

<hr />

**NOTE**

Since we are going to run the indexer multiple times in the next section make sure `IndexDumps()` only indexes 1000 packages:

```csharp
var partitions = var packages = DumpReader.GetPackages().Take(1000);
```

</hr />

Up until this point, you may have noticed that every time we've made a change to our index that required us to reindex our packages, we had to delete the existing index.  That doesn't sound ideal in a production environment. Certainly, deleting an index on a production application should be done with great care...

Elasticsearch has an awesome, and very important feature that can be leveraged to deal with situations like this, called [index aliases](http://www.elastic.co/guide/en/elasticsearch/reference/current/indices-aliases.html).

> The index aliases API allow to alias an index with a name, with all APIs automatically converting the alias name to the actual index name. An alias can also be mapped to more than one index, and when specifying it, the alias will automatically expanded to the aliases indices. An alias can also be associated with a filter that will automatically be applied when searching.

For the folks with a SQL background, you can sort of think of an alias as synonymous with a view in SQL.

So going back to our original problem of reindexing on a live index, how can aliases help here?  Well, instead of pointing directly to the `nusearch` index in our application, we can create an alias that *points* to our index, and have our application use the alias to refer to the index. When we need to reindex data, we can create a new index alongside our current one and reindex our data there. When we're done indexing into the new index, we can swap the alias to point to our newly created index- all without having the change our application code. The operation of swapping aliases is atomic, so the application will not incur any downtime in the process.

This may be better understood through the following illustration:

![Alias swapping](readme-images/alias-swapping.png)

So let's go ahead and implement something close to the above diagram.  First let's create a new property on the `Program` class in `NuSearch.Indexer` to hold our *actual* index name:

```csharp
private static string CurrentIndexName { get; set; }
```

We've already got a handy `CreateIndexName()` method in our `NuSearchConfiguration` class that will create us a `nusearch-` index with the current date and time appended to it.

```csharp
CurrentIndexName = NuSearchConfiguration.CreateIndexName();
```

Next, we're going to introduce 2 aliases: `nusearch`, which will point to our *live* index, and `nusearch-old` which will point to our old indices.

Since we've already been indexing to an actual index named `nusearch`, we're going to have to delete this index first, since you can't have an index and an alias with the same name.  So, let's delete our `nusearch` index first, using Kibana Console 

```
DELETE /nusearch
```

OK, now let's actually implement some code to handle our alias swapping:

```csharp
private static void SwapAlias()
{
	var indexExists = Client.IndexExists(NuSearchConfiguration.LiveIndexAlias).Exists;

	Client.Alias(aliases =>
	{
		if (indexExists)
			aliases.Add(a => a.Alias(NuSearchConfiguration.OldIndexAlias).Index(NuSearchConfiguration.LiveIndexAlias));

		return aliases
			.Remove(a => a.Alias(NuSearchConfiguration.LiveIndexAlias).Index("*"))
			.Add(a => a.Alias(NuSearchConfiguration.LiveIndexAlias).Index(CurrentIndexName));
	});

	var oldIndices = Client.GetIndicesPointingToAlias(NuSearchConfiguration.OldIndexAlias)
		.OrderByDescending(name => name)
		.Skip(2);

	foreach (var oldIndex in oldIndices)
		Client.DeleteIndex(oldIndex);
}
```

A few more things we need to tidy up before running our indexer again...

We need to edit `CreateIndex()` to use our new `IndexName`:

```csharp
Client.CreateIndex(CurrentIndexName, i => i
	// ...
)
```

Easy enough.

Lastly, we need to change our bulk indexing call in `IndexDumps()` to explictly index to `CurrentIndexName`, otherwise it will infer `nusearch` from our connection settings- and that's now our alias!

```csharp
var bulkAll = Client.BulkAll(packages, b => b
	.Index(CurrentIndexName)
	.BackOffRetries(2)
	.BackOffTime("30s")
	.RefreshOnCompleted(true)
	.MaxDegreeOfParallelism(4)
	.Size(1000)
);
```

We can also now get rid of that `DeleteIndexIfExists()` method since we won't be needing it anymore. Our `Main()` should now look like

```csharp
static void Main(string[] args)
{
	Client = NuSearchConfiguration.GetClient();
	string directory = args.Length > 0 && !string.IsNullOrEmpty(args[0]) 
		? args[0] 
		: NuSearchConfiguration.PackagePath;
	DumpReader = new NugetDumpReader(directory);
	CurrentIndexName = NuSearchConfiguration.CreateIndexName();

	CreateIndex();
	IndexDumps();
	SwapAlias();

	Console.WriteLine("Press any key to continue");
	Console.Read();
}
```

Alright, now we're all set!  We can now reindex as many times as we'd like without affecting our live web application.

<hr />

Remember we put that `Take(1)` in place earlier, run the application a couple of times and observe the output of running

```
GET /_cat/aliases?v
```

in Kibana Console

```
alias        index                        filter routing.index routing.search 
nusearch-old nusearch-25-07-2016-05-58-31 -      -             -              
nusearch     nusearch-25-07-2016-05-58-55 -      -             -              
nusearch-old nusearch-25-07-2016-05-58-12 -      -             -              
```

You should see our swapping routing keeps a maximum of two older versions of the index around and points `nusearch` to the latest.

<hr />

Now lets remove that `.Take(1)` in `IndexDumps()` and index one final time so we can see all the packages in the next section.

# Part 3: Searching

### Web architecture

The NuSearch solution ships with a ready made `NuSearch.Web` application which is in fact a console application
that does the actual hosting of the website that we are going to be building.

This might be a strange construct, but it allows us to focus on building the web site without having to configure IIS, Apache or Nginx, or figure out how to run this on other operating systems.

The web project utilizes [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) using the excellent [Kestrel](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel) cross platform webserver.

You are free of course to use whatever architecture in your own application. `NEST 5.x` runs on Desktop CLR and Core CLR, supporting the following frameworks and libraries

- .NET Framework 4.5
- .NET Framework 4.6
- .NET Standard 1.3

Also please note that prior knowledge of any of these is not required to follow along!

You can start the web application simply by navigating to the `NuSearch.Web` directory on the command line and typing

```sh
dotnet run
```

You can then [go to the nusearch web application](http://localhost:5000) on port 5000 (this may be a different port if this conflicts. The port number should be echo'ed to the console output).

See the [troubleshooting section](#Troubleshooting) if you run into issues.

<hr />

If all is well you should now see this:

![First run](readme-images/1-first-run.png)

### Getting started

We've already done most of the plumbing for this web application, so let's talk about what is already in place.

 Folder     | Purpose
------------|-----------
Controllers | MVC controllers that respond to requests
Models      | ViewModels for requests and responses
Views       | The views to render HTML in the response
wwwroot     | Static content such as JavaScript and CSS files. You'll need to uncomment a thing or two as we progress.

### The Search Controller

Our search interface is handled by one `action` defined in the `SearchController` class inside `Controllers`

```csharp
public class SearchController : Controller
{
	private readonly IElasticClient _client;

	public SearchController(IElasticClient client) => _client = client;

	[HttpGet]
	public IActionResult Index(SearchForm form)
	{
		// You'll write most of the implementation here

		var model = new SearchViewModel();

		return View(model);
	}
}
```

`SearchForm` describes the parameters we are going to be sending to control our search from the user interface.
`SearchViewModel` is the model used to build the search results.

### Let's search!

Okay, enough jibber jabber, let's write some code!

In order to search we first need to get a hold of a client for our controller action to use. Thankfully, this has already been wired up for us; an instance of `IElasticClient` is passed to the constructor of our controller and assigned to the `_client` field for us to use.

We can thus issue a search for all our packages using `_client`

```csharp
	var result = _client.Search<Package>(s => s);
```

NEST has a fluent lambda expression syntax which you can see in use here with `s => s`.
What's going on here? Well, you are describing a function  that receives a `SearchDescriptor<Package>()`
referred to with `s`, which can be modified to alter its state in a fluent fashion, changing the structure of the search that will be issued to Elasticsearch.

This begs the question: **Why would you do this??**. Well this fluent lambda expression syntax takes the cognitive burden out of instantating deeply nested object graphs to build up requests, making writing them much more terse. The Elasticsearch APIs and the corresponding query Domain Specific Language (DSL) is rich and can accept many different parameters, so instantiating a new request type can become big pretty quick.

The fluent lambda expression syntax is the preferred way of using the DSL but please
know **it's not the only way**. You can equally write

```csharp
	var result = _client.Search<Package>(new SearchRequest());
```

We call this syntax the `Object Initializer Syntax` and it is fully supported throughout the client. In this tutorial however,
we will be using the `Fluent API Syntax` exclusively.

Ok, so back to our search code so far:

```csharp
	var result = _client.Search<Package>(s => s);
```

This will do a `POST` on `/nusearch/package/_search` with the following JSON:

```json
{}
```

The `ISearchResponse<T>` returned and assigned to `result` allows us to pass the documents we've received from Elasticsearch to our ViewModel.
We'll also inform our ViewModel how many total results Elasticsearch found. We didn't actually specify how many
items we wanted to be returned in our search so Elasticsearch returns `10` by default. However
`result.Total` will actually return how many documents matched our search. In this case all of our packages.

```csharp
var model = new SearchViewModel
{
	Packages = result.Documents,
	Total = result.Total
};
```

`Packages` is commented out in `SearchViewModel.cs` as well as the `UrlFor()` method because at the start of the tutorial we had no `Package`.
Uncomment them now.

We are also going to pass the user input `form` to the viewmodel so it knows what was requested

```csharp
var model = new SearchViewModel
{
	Packages = result.Documents,
	Total = result.Total,
	Form = form
};
```

If we now run our application, lo and behold we have some data!

![Some data](readme-images/2-some-data.png)

If we crack open our `Search/Index.cshtml`, we have two templates for when we do and do not have results:

```csharp
@Html.Partial(Model.Total > 0 ? "Results" : "NoResults", Model)
```

Later on we'll spend more time inside the `Shared/Results.cshtml` partial view. As a final exercise instead of relying on the default `10` items, let's explicitly ask for `25` items.

```csharp
	var result = _client.Search<Package>(s => s
		.Size(25)
	);
```

Here we start to see the `Fluent API Syntax` in action.

### Implementing a query

At this point, we are effectively doing [`match_all` query](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-match-all-query.html) and selecting the top 25 results.
Searching for something in the search box does not do anything.

If you type a search, notice how you are submitting a form but instead of using `POST` this form uses `GET`.

This causes the entered query to be part of the url we go to after the post.

![query string](readme-images/3-show-query-string.png)

In our `SearchController`, whatever is passed on the query string is bound
to an instance `SearchForm` class by the ASP.NET Core framework and passed to our action method

```csharp
[HttpGet]
public IActionResult Index(SearchForm form)
{
	// ...
}
```

This means we can use `form.Query` to access whatever the user typed in to the search box.

Let's feed this to our search

```csharp
var result = client.Search<Package>(s => s
	.Size(25)
	.Query(q => q
		.QueryString(qs => qs
			.Query(form.Query)
		)
	)
);
```

*wowzers*, the complexity of this method has escalated quickly. What's going on here?

NEST is a one-to-one mapping to the Elasticsearch API, so to do a [`query_string` query](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-query-string-query.html) in `Elasticsearch`, you have to send the following `JSON`

```json
{
  "query": {
    "query_string": {
      "query": "hello"
    }
  }
}
```

As you can see the `NEST` DSL follows the structure of the query JSON. It's important to keep in mind that NEST is not in the business of abstracting the Elasticsearch API. In the cases where a shortcut exists, it's never at the cost of **not** exposing the longer notation.

At this point I would like to clue you in on a handy trick if you have installed [Fiddler](https://www.telerik.com/download/fiddler). If it is running, the plumbing of this application automatically uses it as proxy. **NOTE** fiddler has to be running before you start `NuSearch.Web`.

![fiddler](readme-images/4-fiddler.png)

If you want to always see pretty responses from Elasticsearch, update `NuSearchConfiguration.cs` and add `.PrettyJson()` to the `ConnectionSettings()`

### Exploring querying further

So right now we have a fully working search implementation, but how does Elasticsearch know what fields to search?  Well by default, Elasticsearch cheats a little.

When indexing a document, all the values from all the fields of the document are aggregated and also indexed under the special [`_all`](https://www.elastic.co/guide/en/elasticsearch/reference/current/mapping-all-field.html) field.

If you do not specify a field name explicitly in a search request, then queries that allow you to not specify a field name will default to using the `_all` field. Pretty neat, right?

Another thing to note about the [`query_string` query](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-query-string-query.html) we've used is that it exposes the [Lucene query syntax](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-query-string-query.html#query-string-syntax) to the user.

Try some of the following queries in `NuSearch`:

* `id:hello*`
* `id:hello* OR id:autofac^1000`

What's going on here? In the first query, we explicitly search for only those `Package`'s whose `id` starts with `hello`. In the second example, we extend this query to also include `autofac`, but boost it with a factor of `1000` so that it appears first in the result set.

Do you want to expose this kind of query power to the end user? Consider the following:

* `downloadCount:10`

This will search all the packages with a download count of `10` pretty straight forward.

However, If we search for:

* `downloadCount:X`

then we get no results. This makes sense, but Elasticsearch is actually throwing an exception in this case

```json
{
  "error" : "SearchPhaseExecutionException[Failed to execute phase [query], ..."
  "status" : 400
}
```

If you remember the Fiddler tip earlier, you can witness this for yourself by running this query

Elasticsearch has a *better* `query_string` query called
[`simple_query_string` query](http://www.elastic.co/guide/en/elasticsearch/reference/master/query-dsl-simple-query-string-query.html) which will never throw an exception when invalid input is encountered and ignores invalid parts.

Yet the question remains, do you need to expose this power to the end user? Quite often the answer is _NO_.

### The `[multi_]match` query

The match query family is a very powerful one. It will *do the right thing* based on what field it's targeting.

Let's use the [`multi_match` query](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-multi-match-query.html) to apply our search term(s) to several fields we actually want to participate in the search:

```csharp
.Query(q => q
	.MultiMatch(m => m
		.Fields(f => f
			.Fields(
				p => p.Id, 
				p => p.Summary)
			)
		.Query(form.Query)
	)
)
```

Now we are searching through `id` and `summary` fields only, instead of the special `_all` field.  Let's look at the results:

![tdidf](readme-images/5-tfidf.png)

It almost looks as if matches on `id` are more important then those on `summary`, but as the highlighted result demonstrates, something else is actually going on...

### Relevancy scoring

By default `Elasticsearch` uses [BM25](https://en.wikipedia.org/wiki/Okapi_BM25), a probabilistic frameqwork approach to relevancy scoring of documents. That is, it uses probabilistic methods to determine how well a particular document matches a given query.

![BM25 equation](readme-images/bm25_equation.png)

Don't be put off by the mathematical notation of BM25! Prior the Elasticsearch 5.0, another relevancy scoring algorithm, [TF/IDF](http://tfidf.com/), was used to score documents for how well they matched a given query, where 

#### Term Frequency (TF)

- **TF(t)** = (Number of times a given term `t` appears in a document) / (Total number of terms in the document).

#### Inverse Document Frequency (IDF)

- **IDF(t)** = log_e(Total number of documents / Number of documents with term `t` in it).

TF/IDF favours hits on rarer terms in the index, and hits in shorter fields outweigh hits in longer fields.

BM25 is supposed to work better than TF/IDF for short fields such as those that contains names and allows finer grain control over how TF and IDF affect overall relevancy scores. Because of this, it replaced TD/IDF as the default similarity. 

BM25 degrades the IDF score of a term more rapidly as it appears more frequently within the document corpus, and can in fact give a negative IDF score for a highly frequent term across documents (although in practice it is often useful to have a lower score bound of 0,).

![BM25 vs. TF/IDF document frequency](readme-images/bm25_document_freq.png)

BM25 also limits the influence of the term frequency, using the **k** parameter (referred to as `k1` within the Elasticsearch documentation)

![BM25 vs. TF/IDF term frequency](readme-images/bm25_term_freq.png)

With TF/IDF, the more frequent the term appears within an individual document, the higher the weight score given to that term. With BM25, the `k` parameter can control this influence.

Similarly, BM25 allows finer grain control over the document length than TF/IDF, using the `b` parameter

![BM25 vs. TF/IDF document length](readme-images/bm25_document_length.png)

More concretely a hit for `elasticsearch` in `id` is usually scored higher then a hit in `summary` since in most cases `id` is a field with less terms overall.

If we had searched `elasticsearch client` the `elasticsearch` keyword would be higher because its IDF is higher than `client` since the entire index contains the term `client` more than it does `elasticsearch`.

This explains why the highlighted packages `Nest.Connection.Thrift` and `Terradue.ElasticCas` score higher than some of the other packages with `elasticsearch` in the id.They have very short `summary` fields. `Nest` and `Nest.Signed` both mention `elasticsearch` in the `summary` and therefore score higher than `Glimpse.Elasticsearch`

<hr />

**NOTE**

In closing, relevancy scoring is a complex and well researched problem in the information retrieval space. Our explanation of how Elasticsearch handles relevancy is a tad simplified but if you are interested in learning more of [the theory behind scoring](http://www.elastic.co/guide/en/elasticsearch/guide/master/scoring-theory.html) and how
[Lucene takes a practical approach TD/IDF and the vector space model](http://www.elastic.co/guide/en/elasticsearch/guide/master/scoring-theory.html), the Elasticsearch guide has got you covered!. Also know that Elasticsearch allows you to [plug in custom similarity scorers](http://www.elastic.co/guide/en/elasticsearch/reference/1.4/index-modules-similarity.html).

<hr />

### Analysis

So I have to clue you in to another fact: the only packages matching on `id` are the ones highlighted in blue:

![id matches](readme-images/6-id-matches.png)

I hear you say, "*OH NOES!*" but don't worry, this is by design. Let's zoom out a bit and look at what's happening when you index.

Let's index these two fictitious documents:

```json
{
   "id":1,
   "name": "Elasticsearch-Azure-PAAS",
   "author": {
        "name" : "garvincasimir and team",
        "country" : "Unknown",
   }
}
```

```json
{
   "id":2,
   "name": "Elasticsearch.Net",
   "author": {
        "name" : "Elasticsearch .NET team",
        "country" : "Netherlands",
   }
}
```
Elasticsearch will actually represent this document as an inverted index:

| field      | terms             | documents
|------------|-------------------|------------
|name        | elasticsearch     | 1
|            | azure             | 1
|            | paas              | 1
|            | elasticsearch.net | 2
|author.name | team              | 1,2
| `....`     | `...`             | `...`

JSON property values are fed through an index analysis pipeline, which extracts terms from each value.

Fields can have different analyzers, but out of the box, all string properties use the [standard analyzer](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-standard-analyzer.html).

The standard analyzer tokenizes the text based on the Unicode Text Segmentation algorithm, as specified
[in Unicode Standard Annex #29.](http://unicode.org/reports/tr29/).

This surprisingly dictates not to split words conjoined by a dot.

The standard analyzer furthermore lowercases tokens and removes English stop words (highly common words such as `the`, `and`).

The resulting tokens that come out of the index analysis pipeline are stored as `terms` in the inverted index.

You can easily see how the standard analyzer acts on a stream of text by calling the [Analyze API](https://www.elastic.co/guide/en/elasticsearch/reference/current/indices-analyze.html) in Elasticsearch

```
GET /_analyze?pretty=true&analyzer=default&text=Elasticsearch.NET
```

returns

```json
{
  "tokens" : [ {
    "token" : "elasticsearch.net",
    "start_offset" : 0,
    "end_offset" : 17,
    "type" : "<ALPHANUM>",
    "position" : 1
  } ]
}
```

and

```
GET /_analyze?pretty=true&text=Elasticsearch-Azure-PAAS
```

returns

```json
{
  "tokens" : [ {
    "token" : "elasticsearch",
    "start_offset" : 0,
    "end_offset" : 13,
    "type" : "<ALPHANUM>",
    "position" : 1
  }, {
    "token" : "azure",
    "start_offset" : 14,
    "end_offset" : 19,
    "type" : "<ALPHANUM>",
    "position" : 2
  }, {
    "token" : "paas",
    "start_offset" : 20,
    "end_offset" : 24,
    "type" : "<ALPHANUM>",
    "position" : 3
  } ]
}
```

We can clearly see now that `Elasticsearch.NET` is **NOT** broken up but `Elasticsearch-Azure-PAAS` is.

### Query analysis

We've now shown you how Elasticsearch creates terms from your JSON property values. Another process happens at query time. Some, but **not all** query constructs also perform analysis on the query they receive to construct the correct terms to locate.

The [multi_match query](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-multi-match-query.html) we are using right now is one of those constructs that analyzes the query it receives.

If we search for `Elasticsearch-Azure-PAAS` the [standard analyzer](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-standard-analyzer.html) again kicks in and Elasticsearch will actually use
`elasticsearch`, `azure` and `paas` to locate documents in its inverted index that contain any of these terms.

The [`match` family of queries](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-match-query.html) default to using `OR` on the terms.  Being inclusive or exclusive in your search results is another hot topic with no right answer. I personally find being exclusive a whole lot more useful. For instance, when I search for `Elasticsearch-Azure-PAAS` I do not want any results that only have the terms `azure` **and** `paas`.

Lets update our search to use `AND` on the resulting terms we provide

```
.Query(q => q
	.MultiMatch(m => m
		.Fields(f=>f.Fields(p => p.Id, p => p.Summary))
		.Operator(Operator.And)
		.Query(form.Query)
	)
)
```

*Whoohoo!* Now when we search for `Elasticsearch-Azure-SAAS` we only get one result minus all the noise of Azure packages that have nothing
do with Elasticsearch.

Knowing how the analysis chain works both at index and query time, can you answer the following question?

**Will the following document surface using our `multi_match` query on id and summary using the query `Elasticsearch`?**

```json
{
   "id": "Microsoft.Experimental.Azure.ElasticSearch",
   "summary": "This package can deploy Elastic Search on an Azure worker node."
}
```

Obviously the `standard` analyzer is not cutting it for our data.

### Defining analyzers

Quite often the `standard` analyzer works great for regular plain English corpuses but as we've shown, it won't always work for your domain metadata.

Ideally we would want to search on NuGet packages as followed, in order of importance

1. **Exact match**: an exact match on id should always be the first hit.
2. **Better matches of logical parts of the id**: id's should also be broken up by dots and even capitalization (e.g we want the `module` out of `ScriptCs.ShebangModule`).
3. Full text search on summary and version descriptions.

### The Analysis process

Elasticsearch allows you to build your own analysis chain to find the right terms to represent a body of text

![Analysis Chain](readme-images/analysis_chain.png)

* [character filters](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-charfilters.html) preprocess the text before its handed of to the tokenizer, handy for e.g stripping out html prior to tokenizing.
* [tokenizer](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-tokenizers.html) an analysis chain can have only 1 tokenizer to cut the provided string up in to terms.
* [token filters](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-tokenfilters.html) operate on each indivual token and unlike the name might imply these `filter` are not just able to remove terms but also replace or inject more terms.

So knowing the defaults don't cut it for our Nuget `id`s, let's set up some analysis chains when we create the index:

The following is specified as part of the call to `.Settings()` within the `CreateIndex()` method

```csharp
.Analysis(analysis=>analysis
	.Tokenizers(tokenizers=>tokenizers
		.Pattern("nuget-id-tokenizer", p => p.Pattern(@"\W+"))
	)
	.TokenFilters(tokenfilters => tokenfilters
		.WordDelimiter("nuget-id-words", w => w
			.SplitOnCaseChange()
			.PreserveOriginal()
			.SplitOnNumerics()
			.GenerateNumberParts(false)
			.GenerateWordParts()
		)
	)
	.Analyzers(analyzers=>analyzers
		.Custom("nuget-id-analyzer", c => c
			.Tokenizer("nuget-id-tokenizer")
			.Filters("nuget-id-words", "lowercase")
		)
		.Custom("nuget-id-keyword", c => c
			.Tokenizer("keyword")
			.Filters("lowercase")
		)
	)
)
```

Quite a lot is going on here so let's break it down a tad.


```csharp
.Analysis(analysis => analysis
	.Tokenizers(tokenizers => tokenizers
		.Pattern("nuget-id-tokenizer", p=>p.Pattern(@"\W+"))
	)
```

Since the default tokenizer does not split `Elasticsearch.Net` into two terms as previously shown we'll register a [PatternTokenizer](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-pattern-tokenizer.html)
that splits on any non word character. We call this instance of the `PatternTokenizer` `nuget-id-tokenizer` so we can reference it later.

```csharp
.TokenFilters(tokenfilters => tokenfilters
	.WordDelimiter("nuget-id-words", w=> w
		.SplitOnCaseChange()
		.PreserveOriginal()
		.SplitOnNumerics()
		.GenerateNumberParts(false)
		.GenerateWordParts()
	)
)
```

Here we set up a [WordDelimiterTokenFilter](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-word-delimiter-tokenfilter.html) which will further cut terms if they look like two words that are conjoined.

e.g `ShebangModule` will be cut up into `Shebang` and `Module` and because we tell it to preserve the original token, `ShebangModule` as a whole is kept as well.

Now that we've registered our custom [Tokenizer](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-tokenizers.html) and [TokenFilter](https://www.elastic.co/guide/en/elasticsearch/reference/current/analysis-tokenfilters.html), we can create a custom analyzer that will utilize these

```csharp
.Analyzers(analyzers => analyzers
	.Custom("nuget-id-analyzer", c => c
		.Tokenizer("nuget-id-tokenizer")
		.Filters("nuget-id-words", "lowercase")
	)
```

Here we create an analyzer named `nuget-id-analyzer` which will split the input using our `nuget-id-tokenizer` and then filters the resulting terms
using our `nuget-id-words` filter (which will actually inject more terms) and then we use the build in `lowercase` tokenfilter which replaces all the
terms with their lowercase counterparts.

```csharp
	.Custom("nuget-id-keyword", c => c
		.Tokenizer("keyword")
		.Filters("lowercase")
	)
)
```

Here we create a special `nuget-id-keyword` analyzer. The built in [Keyword Tokenizer]() simply emits the text as provided as a single token. We
then use the `lowercase` filter to lowercase the nuget id as a whole. This will allow us to boost exact matches higher without the user having
to know the correct casing of the nuget id.

Now that we have registered our custom analyzers we need to update our mapping so that the Id property uses them.

```csharp
.Text(s=>s
	.Name(p=>p.Id)
	.Analyzer("nuget-id-analyzer")
	.Fields(f => f
		.Text(p => p.Name("keyword").Analyzer("nuget-id-keyword"))
		.Keyword(p => p.Name("raw"))
	)
)
```

Here we setup our `Id` property as a [Multifield mapping](). This allows you to index a field once but elasticsearch will create additional fields in
the inverted index that we can target during search later on.

At index time `id` will use our `nuget-id-analyzer` to split the id into the proper terms, but also when we do a `match` query on the `id` field
elasticsearch will also use our `nuget-id-analyzer` to generate the correct terms from the provided query.


When we query `id.keyword` using the `match` query elasticsearch will simply use the whole query and lowercase it and use that as the term to look
for inside the inverted index for `id.keyword`

We also create a field in the inverted index called `id.raw` which is simply **not** analyzed and stored **as is**. This makes this field very well suited
to sorting (remember lowercasing might change sort order in some locales) and aggregations.

Now that we have our new mapping in place make sure you reindex so that we can take advantage of our new analysis.


##Updating our search

Since we now have a [Multifield mapping](https://www.elastic.co/guide/en/elasticsearch/reference/current/multi-fields.html) set up for our `Id` property we differentiate between an exact lowercase match and a match in our analyzed field.
Furthermore we can make sure a match in our `Summary` field has a lower weight in the overall score of our matching document.

```csharp
.MultiMatch(m => m
	.Fields(f => f
		.Field(p => p.Id.Suffix("keyword"), 1.5)
		.Field(p => p.Id, 1.5)
		.Field(p => p.Summary, 0.8)
	)
	.Operator(Operator.And)
	.Query(form.Query)
)
```

Note: the `.Suffix("")` notation is a great way to reference the fields defined using a multi_field mapping without fully giving up on strongly typed
property references.

If we now search for `elasticsearch` we'll see all the packages we missed earlier, such as the aforementioned
`Microsoft.Experimental.Azure.ElasticSearch` because of the faulty tokenization are now included!

![moar results](readme-images/7-more-search-results.png)

Looking at results however we see an adverse effect in that strong boost on id will push down popular packages. In this case `NEST` is the official
.NET client for `Elasticsearch` but because it has a clever name and only mentions elasticsearch in its summary, its penalized by being at the bottom of the search results. With well over a 100k downloads, its the most downloaded package in our results set.

What we want to do is given a search take a given document's downloadCount into account.

```csharp
.Query(q => q
	.FunctionScore(fs => fs
		.MaxBoost(10)
		.Functions(ff => ff
			.FieldValueFactor(fvf => fvf
				.Field(p => p.DownloadCount)
				.Factor(0.0001)
			)
		)
		.Query(query => query
			.MultiMatch(m => m
				.Fields(f => f
					.Field(p => p.Id.Suffix("keyword"), 1.5)
					.Field(p => p.Id, 1.5)
					.Field(p => p.Summary, 0.8)
				)
				.Operator(Operator.And)
				.Query(form.Query)
			)
		)
	)
)
```

The [function score](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-function-score-query.html) query is the one stop query if you want to influence score beyond BM25 and boosting.

Here we use `field value factor` with a factor of `0.0001` over each documents `downloadCount` combined with its query score

This equates to

```
(0.0001 * downloadCount) * score of query
``` 

Meaning that for packages with low download counts the actual relevance score
is a major differentatior while still acting as a high signal booster for packages with high download counts. 
MaxBoost caps the boostfactor to `10` so that packages of over 100k downloads do not differentiate on downloadCount either.

*NOTE* This is a *very* rudimentary function score query, have a [read through the documentation](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-function-score-query.html) and see if you can come up with something more clever!

When we now look at our results:

![popularity elasticsearch](readme-images/8-popularity-results-elasticsearch.png)

`NEST` and `Elasticsearch.Net` have succesfully surfaced to the top we can also see that this is not simply a sort on downloadcount because
`Elasticsearch.Net` is the number one result despite the fact it has less downloads then `NEST`.

## Combining multiple queries

Elasticsearch exposes [bool queries and filters](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-bool-query.html), but these do not quite equate to the boolean logic you are use to in C#. NEST allows you
to [use the operators `&&` `||`, `!` and `+`](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/bool-queries.html) to combine queries into Elasticsearch boolean queries but NEST will combine them in such a way that they do follow the exact same boolean logic you are use to in C#.

Having fixed surfacing popular packages for keywords, we've broken exact matches on `Id` being most important. As can be seen by doing a query for `antlr4`

![antlr4 second](readme-images/9-antlr4-second.png)

```csharp
.Query(q =>
	q.Match(m=>m.Field(p=>p.Id.Suffix("keyword")).Boost(1000).Query(form.Query))
	|| q.FunctionScore(fs => fs
		.Functions(ff => ff
			.FieldValueFactor(fvf => fvf
				.Field(p => p.DownloadCount)
	//snip
```

Here we use the binary `||` operator to create a bool query to match either on our keyword multi_field and if it does, give it a ridiculous boost,
or otherwise it will have to match with our function score query. Note that you can remove the `p.Id.Suffix` from your `MultiMatch` query now.

![antlr4 first](readme-images/10-antlr4-first.png)

## Conditionless queries

`NEST` has a feature turned on by default called **conditionless queries** which will rewrite incomplete queries out of the actual query that is sent to Elasticsearch. Given that our query now looks like this:

```csharp
.Query(q => q
	.Match(m => m
		.Field(p => p.Id.Suffix("keyword"))
		.Boost(1000)
		.Query(form.Query)
	) || q
	.FunctionScore(fs => fs
		.Functions(ff => ff
			.FieldValueFactor(fvf => fvf
				.Field(p => p.DownloadCount)
				.Factor(0.0001)
			)
		)
		.Query(query => query
			.MultiMatch(m => m
				.Fields(f => f
				      .Field(p => p.Id, 1.2)
				      .Field(p => p.Summary, 0.8)
                                )
				.Operator(Operator.And)
				.Query(form.Query)
			)
		)
	)
)
```

What happens if `form.Query` is actually `null` or `empty`? Well in that case

1. The first match query on the left side of the `||` operator will mark itself as `conditionless`
2. The `function_score` on the right side of the operator will mark its `query` property as conditionless but because it has functions it won't actually mark itself.
3. The `||` operator handling notices there is only one resulting query (the `function_score`) and won't wrap it in an unneeded bool query.

If you are following along with fiddler you can see that in case of an empty `form.Query` `NEST` will send the following query to Elasticsearch:

```json
{
  "size": 25,
  "query": {
    "function_score": {
      "functions": [
        {
          "field_value_factor": {
            "field": "downloadCount",
            "factor": 0.0001
          }
        }
      ]
    }
  }
}
```

If this is not intended behavior you can control that through either IsVerbatim, meaning we will write the query verbatim as it was specified. 
Empty strings and all, or IsStrict which would cause an error to be thrown if a query ends up conditionless.


```csharp
.Query(q => q
	.Verbatim(true)
	.Match(m => m
		.Field(p => p.Id.Suffix("keyword"))
		.Boost(1000)
		.Query(form.Query)
	) || q
	.Strict()
	.FunctionScore(fs => fs
```
As a final reminder that if the fluent syntax is not working out for you here would be the equivalent in a more classical object initializer syntax

```csharp
var result = client.Search<Package>(new SearchRequest<Package>
{

	Size = 25,
	Query =
		new MatchQuery 
		{ 
			Field = Field<Package>(p => p.Id.Suffix("keyword")), 
			Boost = 1000, 
			Query = form.Query, 
		}
		|| new FunctionScoreQuery
		{
			MaxBoost = 10,
			Functions = new List<IScoreFunction>
			{
				new FieldValueFactorFunction
				{
					Field = Field<Package>(p=>p.DownloadCount),
					Factor = 0.0001
				}
			},
			Query = new MultiMatchQuery
			{
				Query = form.Query,
				Operator = Operator.And,
				Fields = Field<Package>(p => p.Id.Suffix("keyword"), 1.5)
					.And("id^0.8")
					.And<Package>(p => p.Summary, 0.8)
			}
		}
});
```

Which will behave exactly the same as the fluent example earlier.


----
# Part 4: Paging & Sorting

Now that we have a handle on how the searching works let's quickly add sort pagination and sorting to our website so we can easily browse the resulting packages

Uncomment the following in `Search.cshtml` twice

```cshtml
	@*	@Html.Partial("Partials/Pager", Model)*@
```

In `SearchModule` lets make sure our model knows how many pages we expect

```csharp
model.TotalPages = (int)Math.Ceiling(result.Total / (double)form.PageSize);
```

And lets update our search so that we actually take into account the pagination values

```
.From((form.Page - 1) * form.PageSize)
.Size(form.PageSize)
```

We now have a very crude pagination on our nusearch website!

![pagination](readme-images/pagination.png)


In order to control the sort order and page size, uncomment the following lines in `Results.cshtml`

```cshtml
@*@Html.Partial("Partials/Sorting", Model)*@
@*@Html.Partial("Partials/PageSize", Model)*@
```

and add the following to our search to change to sort order:

```csharp
.Sort(sort =>
{
	if (form.Sort == SearchSort.Downloads)
		return sort.Descending(p => p.DownloadCount);
	else if (form.Sort == SearchSort.Recent)
		return sort.Field(sortField => sortField
			.NestedPath(p => p.Versions)
			.Field(p => p.Versions.First().LastUpdated)
			.Descending()
		);
	else return sort.Descending("_score");
})
```

if the sort order is downloads we do a descending sort on our downloadcount field.

If the sort order is most recently updated we need to a descending `nested sort` on `p.Versions.First().LastUpdated` because we mapped `Versions` as a nested object array in the previous module.

Otherwise we sort descending by `"_score"`, which is the default behaviour. Returning `null` here is also an option.

You should now see the following options in the search criteria box:


![pagination](readme-images/search-criteria.png)

The JavaScript to listen to selection changes should already be in place.

Happy exploring!

----

# Part 5: Aggregations

When we say Elasticsearch isn't just a search engine, but also an analytics engine, we are mainly talking about [aggregations](http://www.elastic.co/guide/en/elasticsearch/reference/current/search-aggregations.html).  Aggregations are a very powerful feature in Elasticsearch that allow you to analyze your data, build faceted searches, and reap the benefits of the near-real time nature of Elasticsearch.

There are many different aggregations available for use in Elasticsearch, each falling into two categories:

**1) Bucket aggregations**
> A family of aggregations that build buckets, where each bucket is associated with a key and a document criterion. When the aggregation is executed, all the buckets criteria are evaluated on every document in the context and when a criterion matches, the document is considered to "fall in" the relevant bucket. By the end of the aggregation process, we’ll end up with a list of buckets - each one with a set of documents that "belong" to it.

An example of a bucket aggregation is the [terms aggregation](http://www.elastic.co/guide/en/elasticsearch/reference/current/search-aggregations-bucket-terms-aggregation.html) which builds a bucket for each unique term in your index along with the number of times it occurs.  We are going to explore the terms aggregation further in this workshop.

**2) Metric aggregations**
> Aggregations that keep track and compute metrics over a set of documents.

A few examples of metric aggregations are the [min](http://www.elastic.co/guide/en/elasticsearch/reference/current/search-aggregations-metrics-min-aggregation.html) and [max](http://www.elastic.co/guide/en/elasticsearch/reference/current/search-aggregations-metrics-max-aggregation.html) aggregations, which simply compute, well, the minimum and maximum value of a field.

## Faceted search

As mentioned earlier, aggregations make it very easy to build a faceted search interface.  What do we mean by faceted search?  Well, have you ever been on Amazon and have used the nav on the left-hand side to narrow down your search results?

![facets-example](readme-images/facets-example.png)

This left hand side nav that allows you to narrow down your search results by selecting different criteria while at the same time also showing the number of results for each selection is known as faceted searching, and it's a very useful and intuitive way for a user to navigate through data.

There are many aggregations that we could use to build out our faceted search, but for the sake of this workshop we're going to focus on the simple terms aggregation.  Let's create a terms aggregation that will run over our author names and allow our users to narrow search results by author.

First, let's take a look at the aggregation DSL in Elasticsearch for a terms aggregation:

```json
{
  "query": {
    "match_all": {}
  },
  "aggs" : {
    "author-names" : {
      "terms" : { "field" : "authors.name" }
    }
  }
}
```

Aggregations are just another search tool, so just likes queries, they are executed against the `_search` endpoint.

So as part of our search request, adjacent to the query portion we now have an `aggs` portion where we can define N number of aggregations.  In this case we are only defining a single terms aggregation.

`author-names` here is our user-defined name that we are assigning to this particular terms aggregation which we will use later to pull out the results from the response.

Inside of `author-names` is the Elasticsearch reserved `terms` token which tells Elasticsearch to execute a terms aggregation, and inside the terms aggregation body we specify the field we want to the aggregation to run over, which in this case is the `authors.name` field.

Now notice the `match_all` query that we are executing along with this aggregation.  **Aggregations always execute over the document set that the query returns**.  So in this case, our terms aggregation is going to run over **all** the documents in our index since we're doing a `match_all` (this is sort of a contrived example because we could have simply left out the query portion altogether in this case- which would also act as a `match_all`).  We could have instead, only ran our aggregations over, say, packages that have a download count of over 1000 by substituting a [range filter](http://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-range-filter.html) on our `downloadCount` field rather than a `match_all`.

Wait a second here though, there's something wrong.

What's the problem?

Remember, our `author` type (`PackageAuthor`) is nested!  We need to treat it as a nested object much like we do in our search queries.  So let's change it up a bit:

```json
{
  "query": {
    "match_all": {}
  },
  "aggs": {
    "authors": {
      "nested": {
        "path": "authors"
      },
      "aggs": {
        "author_names": {
          "terms": { "field": "authors.name.raw" }
        }
      }
    }
  }
}
```

Now if we run this we should get the following response:

```json
{
   "took": 88,
   "timed_out": false,
   "_shards": {
      "total": 2,
      "successful": 2,
      "failed": 0
   },
   "hits": {
      "total": 40740,
      "max_score": 1,
      "hits": [...]
   },
   "aggregations": {
      "authors": {
         "doc_count": 40747,
         "author_names": {
            "doc_count_error_upper_bound": 342,
            "sum_other_doc_count": 79465,
            "buckets": [
               {
                  "key": "microsoft",
                  "doc_count": 2182
               },
               {
                  "key": "jason",
                  "doc_count": 952
               },
               {
                  "key": "inc",
                  "doc_count": 844
               },
               {
                  "key": "jarrett",
                  "doc_count": 836
               },
               {
                  "key": "james",
                  "doc_count": 813
               },
               {
                  "key": "software",
                  "doc_count": 779
               },
               {
                  "key": "robert",
                  "doc_count": 665
               },
               {
                  "key": "daniel",
                  "doc_count": 567
               },
               {
                  "key": "tomas",
                  "doc_count": 547
               },
               {
                  "key": "phillip",
                  "doc_count": 544
               }
            ]
         }
      }
   }
}
```

Notice our `match_all` still returned hits, but now we have an `aggregations` portion of the response that contains the results from our `author_names` terms aggregation.  Each bucket has a `key` which is the value of the `authors.name` field along with the corresponding `doc_count` (frequency of the term) from all the matching documents.

**Pro-tip:** In this example, we returned search hits in addition to aggregation results in a single request.  A lot of times you may only care about getting aggregation results from Elasticsearch and don't necessarily care about search results.  In that case its best to specify `"size" : 0` in the request body.  This will result in the request executing faster since the query will completely bypass the fetch phase.

Alright, so thats how to construct an aggregation using the Elasticseach JSON DSL directly.  Let's implement the same thing, but using NEST.

In our `SearchModule`, let's extend our query and add the terms aggregation:

```csharp
.Aggregations(a => a
	.Nested("authors", n => n
		.Path("authors")
		.Aggregations(aa => aa
			.Terms("author-names", ts => ts
				.Field(p => p.Authors.First().Name.Suffix("raw"))
			)
		)
	)
)
```

This will produce the exact equivalent JSON as above.  Again, notice the one-to-one mapping NEST has to the actual Elasticsearch DSL, it is practically verbatim.

Now let's retrieve the results from the aggregation and populate our `SearchViewModel`:

```csharp
var authors = result.Aggs.Nested("authors")
		.Terms("author-names")
		.Buckets
		.ToDictionary(k => k.Key, v => v.DocCount);

	model.Authors = authors;
```

Lastly, there is an already baked partial for displaying these results, but it has been commented out up until this point.  Let's pull up the 'NuSearch.Web\Search\Partials\Results.cshtml' view and uncomment it:

```csharp
@Html.Partial("Partials/Aggregations", Model)
```

If we run our web app again, you should now have a nav on the right-hand side that contains the results from our terms aggregation!

But hold on again...something's funny going on with our author names.  There's a bunch of lowercased first names, and other generic terms like `inc` and `software`.  WTF?

Let's pause for a second and think about why this could be...

Our author name field is being analyzed with the standard analyzer!  That means an author name like `Elasticsearch, Inc.` is being tokenized and normalized into two separate tokens: `elasticsearch` and `inc`.  Instead, we want `Elasticsearch, Inc.` as one single token in our index that we can match on exactly.  We can achieve this by changing our mapping and telling Elasticsearch to not analyze this field, however that will affect our search.  We still want the text `elasticsearch` on the author name field to return results for `Elasticsearch, Inc.`, right?

To solve this, we can introduce a multi_field on `authors.name` and have an analyzed version that we can search on, and a non-analyzed version that we can aggregate on.

Sound simple enough?  Alright then, let's change up our mapping of `PackageAuthor`:

```csharp
.Properties(pps => pps
	.Text(s => s
		.Name(a => a.Name)
		.Fields(fs => fs
			.Keyword(ss => ss
				.Name("raw")
			)
		)
	)
)
```

So here we are defining a second field using `Fields()` and naming it `raw` as it is a not analyzed version of the field.

Now when we search we reference the main `authors.name` field, but in our terms aggregation we reference the `authors.name.raw` field.

Let's reindex our data once again for the new mapping to take affect.

<hr />

Now let's change our aggregation to refer to the `raw` keyword field:

```csharp
.Field(p => p.Authors.First().Name.Suffix("raw"))
```

And that's it!  If we run our application again you can see the author names are now exactly how they appear in our package documents, and what we expected in the first place.

### Filtering on facet selections

We can add the author facet selection to our query as followed


```csharp
.Query(q => 
	(q.Match(m => m
		.Field(p => p.Id.Suffix("keyword"))
		.Boost(1000)
		.Query(form.Query)
	) || q
	.FunctionScore(fs => fs
		.MaxBoost(10)
		.Functions(ff => ff
			.FieldValueFactor(fvf => fvf
				.Field(p => p.DownloadCount)
				.Factor(0.0001)
			)
		)
		.Query(fq => fq
			.MultiMatch(m => m
				.Fields(f => f
					.Field(p => p.Id.Suffix("keyword"), 1.5)
					.Field(p => p.Id, 1.5)
					.Field(p => p.Summary, 0.8)
				)
				.Operator(Operator.And)
				.Query(form.Query)
			)
		)
	)) && +q
	.Nested(n => n
	    .Path(p => p.Authors)
	    .Query(nq => +nq
			.Term(p => p.Authors.First().Name.Suffix("raw"), form.Author)
		)
	)
)
```

Now we are search for either `exact term match on id.keyword boosted with 1000` *OR* `function scored query on our selected fields` *AND* `nested search on authors name.raw`. 

A keen observer will have spotted a `+` before `q.Nested`; this automatically wraps the query in a bool filter clause to tell Elasticsearch not to calculate a score for the query. 

Similarly, `!` can be used to easily negate a query.

That's all we're going to cover on aggregations in this workshop, but we've only scratched the surface.  We encourage you to experiment with all of the different aggregation types available in Elasticsearch and see what you can come up with to further extend our faceted search!

Have a further browse through [the official documentation on aggregations](https://www.elastic.co/guide/en/elasticsearch/reference/current/search-aggregations.html)

# Part 6: Suggestions

What's an awesome search application without some auto complete?  As a final touch to NuSearch, we're now going to add some auto completion functionality to our search box.

Elasticsearch provides a few APIs for suggestions:  the [term suggester](http://www.elastic.co/guide/en/elasticsearch/reference/current/search-suggesters-term.html) which is handy for suggesting on single terms (think spell correction, or "did you mean" functionality), [phrase suggester](http://www.elastic.co/guide/en/elasticsearch/reference/current/search-suggesters-phrase.html) which is similar to the term suggester except works on entire phrases of text rather than individual terms, and lastly the [completion suggester](http://www.elastic.co/guide/en/elasticsearch/reference/current/search-suggesters-completion.html) for basic autocomplete functionality, which is what we're going to focus on implementing now.

A question you might ask yourself is, if Elasticsearch provides queries like the [prefix query](http://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-prefix-query.html), then why do I need to go through the trouble of using a dedicated suggestion API?  The reason is simply for performance.  The completion suggester is backed by a data structure called an FST (Finite State Transducer), built at **indexing** time, and can return suggestion in sub-milliseconds; exactly what you need when building out a type ahead.

![fst](readme-images/fst.png)

## Setting up the mapping

The completion suggester requires a bit of setup in order to use.

The first thing we need to do is introduce a special `completion` field to our existing mapping for the `package` type.  This field needs to contain a couple of things:

1) A list of inputs that we want to suggest on
4) an optional weight/boost to apply to the suggestion.  This will become a bit clearer once we actually get to implementing it.

 as a property on our `Package` POCO:

```csharp
public CompletionField Suggest { get; set; }
```

Next, we need to map our `Suggest` field as a `completion` type.  So let's add the following to our existing mapping:

```csharp
.Completion(c => c
	.Name(p => p.Suggest)
)
```

## Indexing suggestions

Now that we have our completion field and mapping setup, we need to actually supply Elasticsearch with the data for our suggestions in order for it to build up the FST.

Let's think about the `input` for our suggestions first.  Consider the package `Newtonsoft.Json.Net`.  We'd like to suggest `Newtonsoft.Json.Net` if a user starts typing the beginning characters: `newt`.

Now, `Newtonsoft.Json.Net` being such a popular .NET library, wouldn't it also be useful to also suggest this package if a user simply typed `json` only?

If you take a look at a lot of the NuGet package names/ids, you'll notice that many of them are dot-delimited, so perhaps it makes sense to split on dots and provide each term as an input

So in that case we'd have:

**Input**: `Newtonsoft`, `Json`, `Net`

If a user starts typing any of those input terms, we are going to output the _source for that package.  Make sense so far?

Now imagine there's a very unpopular package out there named `Unpopular.Json.Library`.  It too will have the input term `json`.  However, it wouldn't be very useful to suggest this package since, most likely, it's not what the user is looking for.  So how can we prevent `Unpopular.Json.Library` from being suggested before `Newtonsoft.Json.Net`?

What's that you say?  `Weight`?  Correct!

We can use the `Weight` property of the completion object as a way to boost more popular packages in our suggestions.  The higher the weight, the higher it's boosted.

Okay, that's great, but how do we determine the popularity of a package?  Well, remember we have the download count? :)  I think that's a pretty good indicator of popularity, so let's use that for our weight value.

Now that we have a plan in place for building our completion fields, let's put it to action by modifying the constructor of our `Package` class and adding:

```csharp
this.Suggest = new CompletionField
{
	Input = new List<string>(latestVersion.Id.Split('.')) { latestVersion.Id },
	Weight = latestVersion.DownloadCount
};
```

Now that we have all the plumbing in place for our completion suggestions, let's go ahead and reindex!

<hr />

Once indexing is complete, we can test our suggestions by using Sense by using suggest on the `_search` endpoint. We use [source filtering]() to include only the fields we are interested in from the package source, in this case, `id`, `downloadCount` and `summary`

```json
GET nusearch/package/_search
{
  "suggest": {
    "package-suggestions": {
      "prefix": "newt",
      "completion": {
        "field": "suggest"
      }
    }
  },
  "_source": {
    "includes": [
      "id",
      "downloadCount",
      "summary"
    ]
  }
}
```

Hopefully you get back a response similar to:

```json
{
  "took" : 5,
  "timed_out" : false,
  "_shards" : {
    "total" : 2,
    "successful" : 2,
    "failed" : 0
  },
  "hits" : {
    "total" : 0,
    "max_score" : 0.0,
    "hits" : [ ]
  },
  "suggest" : {
    "package-suggestions" : [
      {
        "text" : "newt",
        "offset" : 0,
        "length" : 4,
        "options" : [
          {
            "text" : "Newtonsoft",
            "_index" : "nusearch-26-10-2016-23-36-19",
            "_type" : "package",
            "_id" : "Newtonsoft.Json",
            "_score" : 1.2430751E7,
            "_source" : {
              "summary" : "Json.NET is a popular high-performance JSON framework for .NET",
              "id" : "Newtonsoft.Json",
              "downloadCount" : 12430751
            }
          },
          {
            "text" : "Newtonsoft",
            "_index" : "nusearch-26-10-2016-23-36-19",
            "_type" : "package",
            "_id" : "Newtonsoft.Json.Glimpse",
            "_score" : 10706.0,
            "_source" : {
              "id" : "Newtonsoft.Json.Glimpse",
              "downloadCount" : 10706
            }
          },
          {
            "text" : "NewtonsoftJson",
            "_index" : "nusearch-26-10-2016-23-36-19",
            "_type" : "package",
            "_id" : "Rebus.NewtonsoftJson",
            "_score" : 5787.0,
            "_source" : {
              "id" : "Rebus.NewtonsoftJson",
              "downloadCount" : 5787
            }
          },
          {
            "text" : "Newtonsoft",
            "_index" : "nusearch-26-10-2016-23-36-19",
            "_type" : "package",
            "_id" : "Newtonsoft.JsonResult",
            "_score" : 4266.0,
            "_source" : {
              "id" : "Newtonsoft.JsonResult",
              "downloadCount" : 4266
            }
          },
          {
            "text" : "Newtonsoft",
            "_index" : "nusearch-26-10-2016-23-36-19",
            "_type" : "package",
            "_id" : "Newtonsoft.Msgpack",
            "_score" : 1649.0,
            "_source" : {
              "id" : "Newtonsoft.Msgpack",
              "downloadCount" : 1649
            }
          }
        ]
      }
    ]
  }
}

```

## Tying to the web app

So we have our suggestions all setup in Elasticsearch.  Let's bring them to life now.

For the actual textbox and front-endy stuff, we're going to use Twitter's awesome [typeahead.js](https://twitter.github.io/typeahead.js/) library.  For the sake of time, we've actually gone ahead and implemented it for you already.  We just need to un-comment the following line in `NuSearch.Web\Static\search.js`:

```javascript
//setupTypeAhead();
```

If you take a look at the actual implementation of `setupTypeAhead()`, you'll notice that it does an ajax post to `/suggest`.  So we need to go and implement that endpoint.

Let's add a new module called `SuggestModule`:

```csharp
namespace NuSearch.Web.Search
{
	public class SuggestModule : NancyModule
	{
		public SuggestModule()
		{

			Post["/suggest"] = x =>
			{
				// TODO: implement
			};
		}
	}
}
```

Next, let's use NEST to actually call the suggest on the `_search` endpoint in Elasticsearch:

```csharp
Post["/suggest"] = x =>
{
	var form = this.Bind<SearchForm>();
	var client = NuSearchConfiguration.GetClient();
	var result = client.Search<Package>(s => s
		.Index<Package>()
		.Source(sf => sf
			.Includes(f => f
				.Field(ff => ff.Id)
				.Field(ff => ff.DownloadCount)
				.Field(ff => ff.Summary)
			)
		)
		.Suggest(su => su
			.Completion("package-suggestions", c => c
				.Prefix(form.Query)
				.Field(p => p.Suggest)
			)
		)

	);

	var suggestions = result.Suggest["package-suggestions"]
		.FirstOrDefault()
		.Options
		.Select(suggest => new
		{
			id = suggest.Source.Id,
			downloadCount = suggest.Source.DownloadCount,
			summary = !string.IsNullOrEmpty(suggest.Source.Summary)
				? string.Concat(suggest.Source.Summary.Take(200))
				: string.Empty
		});

	return Response.AsJson(suggestions);
};
```

So here we're calling `Suggest()` inside of `Search<T>()` which maps to Elasticsearch's suggest on the `_search` API endpoint.  We then specify the index for where our suggestions live (in this case inferring it from our `Package` type), calling `Completion()` to indicate we're executing a completion suggestion, and in which we further describe by specifying the input text from the user, `.Prefix(form.Query)`, and the path to our suggestion field, `.Field(p => p.Suggest)`.

Notice we've assigned the name `package-suggestions` when we made the request, so we'll use that name to retrieve it from the response.  Lastly, we return an array of objects projected from the source in the result and return this as JSON to our `typeahead` control, and that's it!

Let's test this out by typing in our search text box:

![autocomplete](readme-images/autocomplete.png)

Nice!  Now how about that for speed?

<hr />

## Troubleshooting

### Missing NuGet package dependencies

All NuGet package dependencies required for the solution are checked into the repository in the `packages` directory in the root, to make the getting started as easy as possible. That said, if you find there are packages missing, run the following in the `src` directory

```sh
dotnet restore
```

to restore any missing packages.

### Runtime exception with ASP.NET Core

You may see the following exception when running ASP.NET Core and visting the site on `http://localhost:5000`

```
$ dotnet run
Hosting environment: Production
Content root path: <path to ASP.NET Core directory>
Now listening on: http://localhost:5000
Application started. Press Ctrl+C to shut down.
fail: Microsoft.AspNetCore.Server.Kestrel[13]
      Connection id "0HL6EIBRB75A1": An unhandled exception was thrown by the application.
System.InvalidOperationException: Can not find compilation library location for package 'elasticsearch.net'
   at Microsoft.Extensions.DependencyModel.CompilationLibrary.ResolveReferencePaths()
   at System.Linq.Enumerable.<SelectManyIterator>d__159`2.MoveNext()
   at Microsoft.AspNetCore.Mvc.Razor.Compilation.MetadataReferenceFeatureProvider.PopulateFeature(IEnumerable`1 parts, MetadataReferenceFeature feature)
   at Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartManager.PopulateFeature[TFeature](TFeature feature)
   at Microsoft.AspNetCore.Mvc.Razor.Internal.RazorReferenceManager.GetCompilationReferences()
   at System.Threading.LazyInitializer.EnsureInitializedCore[T](T& target, Boolean& initialized, Object& syncLock, Func`1 valueFactory)
   at Microsoft.AspNetCore.Mvc.Razor.Internal.RazorReferenceManager.get_CompilationReferences()
   at Microsoft.AspNetCore.Mvc.Razor.Internal.DefaultRoslynCompilationService.CreateCompilation(String compilationContent, String assemblyName)
   at Microsoft.AspNetCore.Mvc.Razor.Internal.DefaultRoslynCompilationService.Compile(RelativeFileInfo fileInfo, String compilationContent)
   at Microsoft.AspNetCore.Mvc.Razor.Internal.CompilerCache.CreateCacheEntry(String relativePath, String normalizedPath, Func`2 compile)
--- End of stack trace from previous location where exception was thrown ---
```

Due to a bug in the .NET Core runtime in some distributions, the runtime only tries to load assemblies from the global NuGet cache rather than from the local NuGet `packages` directory. To resolve, either

- copy the directories in the `packages` directory into the global NuGet cache directory

or

- remove the `NuGet.config` file from the root of the repository and run

    ```sh
	dotnet restore
	```

	to restore packages into the global NuGet cache.


<hr />
