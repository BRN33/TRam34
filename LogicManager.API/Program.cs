using LogicManager.Application.Features.Tako;
using LogicManager.Domain.Services;
using LogicManager.Infrastructure.Interfaces;
using LogicManager.Infrastructure.Services;
using LogicManager.Persistence.Data;
using LogicManager.Persistence.Interfaces;
using LogicManager.Persistence.Models;
using LogicManager.Persistence.Services;
using LogicManager.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace LogicManager.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var appHost = builder.Configuration.GetSection("ConnectionStrings").GetSection("Urls").Value!;

            builder.WebHost.UseUrls(appHost);//Dýs ip de calýsacaksa bunu acýyoruz

            // Add services to the container.
            builder.Services.AddControllers();

            // Gerekli servislerin DI kaydý


            builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
            builder.Services.AddSingleton<IMongoDbService, MongoDbService>();

            //builder.Services.AddSingleton<ITcmsService, TcmsService>();
            builder.Services.AddSingleton<LeadershipManager>();//Master Slave yapýsý icin

            builder.Services.AddSingleton<IAnonsService, AnonsService>();
            builder.Services.AddSingleton<ILedService, LedService>();
            builder.Services.AddSingleton<ILcdService, LcdService>();
            builder.Services.AddSingleton<ITakoReaderService, TakoReaderService>();
            builder.Services.AddSingleton<IRouteService, RouteService>();
            builder.Services.AddSingleton<ITrainManagement, TrainManagement>();


            builder.Services.AddHostedService<TakoDataCommand>();
            builder.Services.AddHttpClient<TakoReaderService>();
            builder.Services.AddHttpClient<LoggerHelper>();


            //Console da log seviyesini belirleme
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.None); // Sadece HttpClient loglarýný gizler


            builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();


            var app = builder.Build();

            // middleware   katmaný
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
