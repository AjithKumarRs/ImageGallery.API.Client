﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using ImageGallery.API.Client.Service.Configuration;
using ImageGallery.API.Client.Service.Helpers;
using ImageGallery.API.Client.Service.Interface;
using ImageGallery.API.Client.Service.Models;
using ImageGallery.API.Client.Service.Providers;
using ImageGallery.API.Client.Service.Services;
using ImageGallery.FlickrService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImageGallery.API.Client.Console
{
    public class Program
    {
        /// <summary>
        ///
        /// </summary>
        public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .Build();

        public static ITokenProvider TokenProvider { get; set; }

        public static IImageService ImageService { get; set; }

        public static IImageSearchService ImageSearchService { get; set; }

        public static int Main(string[] args) => MainAsync().GetAwaiter().GetResult();

        private static async Task<int> MainAsync()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            IConfiguration configuration = Configuration;
            var login = configuration["imagegallery-api:login"];
            var password = configuration["imagegallery-api:password"];
            var api = configuration["imagegallery-api:api"];
            var imageGalleryApi = configuration["imagegallery-api:uri"];

            var token = await TokenProvider.RequestResourceOwnerPasswordAsync(login, password, api);
            token.Show();

            // Sample 1 - Get Images from Local File System and Upload
            var images = ImageService.GetImages();
            await GoPostList(images, token, imageGalleryApi);

            // Sample 2 - Get Images from Flickr and Upload
            var imageList = await ImageSearchService.GetImagesAsync();
            await GoPostList(imageList, token, imageGalleryApi);

            await GoGet(token, imageGalleryApi);

            System.Console.ReadLine();
            return 0;
        }

        private static async Task<string> GoPostList(IEnumerable<ImageForCreation> images, TokenResponse token, string imageGalleryApi)
        {
            foreach (var image in images)
            {
                System.Console.WriteLine(image.ToString());
                var serializedImageForCreation = JsonConvert.SerializeObject(image);
                using (var client = new HttpClient())
                {
                    client.SetBearerToken(token.AccessToken);
                    var response = await client.PostAsync(
                            $"{imageGalleryApi}/api/images",
                            new StringContent(serializedImageForCreation, System.Text.Encoding.Unicode, "application/json"))
                        .ConfigureAwait(false);
                }
            }

            return "Sucess";
        }

        private static async Task<HttpResponseMessage> GoPost(TokenResponse token, string imageGalleryApi)
        {
            // create an ImageForCreation instance
            var imageForCreation = new ImageForCreation()
            {
                Title = "Test Title",
                Category = "Test Category",
            };

            string appPath = Directory.GetCurrentDirectory();
            string photoPath = @"../../../../../data/photos";
            var filePath = Path.GetFullPath(Path.Combine(appPath, photoPath));
            var fileName = Path.Combine(filePath, "9982986024_0d2a4f9b20_z.jpg");

            using (var fileStream = new FileStream(fileName, FileMode.Open))
            using (var ms = new MemoryStream())
            {
                fileStream.CopyTo(ms);
                imageForCreation.Bytes = ms.ToArray();
            }

            var serializedImageForCreation = JsonConvert.SerializeObject(imageForCreation);

            using (var client = new HttpClient())
            {
                client.SetBearerToken(token.AccessToken);
                var response = await client.PostAsync(
                    $"{imageGalleryApi}/api/images",
                        new StringContent(serializedImageForCreation, System.Text.Encoding.Unicode, "application/json"))
                    .ConfigureAwait(false);

                return response;
            }
        }

        private static async Task<string> GoGet(TokenResponse token, string imageGalleryApi)
        {
            // call api
            var client = new HttpClient();
            client.SetBearerToken(token.AccessToken);

            var response = await client.GetAsync($"{imageGalleryApi}/api/images");
            if (!response.IsSuccessStatusCode)
            {
                System.Console.WriteLine(response.StatusCode);
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                var images = JsonConvert.DeserializeObject<List<ImageModel>>(content);
                System.Console.WriteLine(JArray.Parse(content));
                System.Console.WriteLine($"ImagesCount:{images.Count}");
                return content;
            }

            return null;
        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(new LoggerFactory()
                .AddConsole()
                .AddDebug());
            serviceCollection.AddLogging();

            serviceCollection.AddOptions();
            serviceCollection.Configure<OpenIdConnectConfiguration>(Configuration.GetSection("openIdConnectConfiguration"));

            var openIdConfig = Configuration.GetSection("openIdConnectConfiguration").Get<OpenIdConnectConfiguration>();
            var flickrConfig = Configuration.GetSection("flickrConfiguration").Get<FlickrConfiguration>();

            var serviceProvider = new ServiceCollection()
                .AddScoped<ITokenProvider>(_ => new TokenProvider(openIdConfig))
                .AddScoped<ISearchService>(_ => new SearchService(flickrConfig.ApiKey, flickrConfig.Secret))
                .AddScoped<IImageService, ImageService>()
                .AddScoped<IImageSearchService, ImageSearchService>()
               .BuildServiceProvider();

            TokenProvider = serviceProvider.GetRequiredService<ITokenProvider>();
            ImageService = serviceProvider.GetRequiredService<IImageService>();
            ImageSearchService = serviceProvider.GetRequiredService<IImageSearchService>();
        }
    }
}
