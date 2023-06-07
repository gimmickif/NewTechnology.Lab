using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApi.Redis;
using WebApi.Services.Book;
using WebApi.Services.Inventory;
using WebApi.RabbitMq;
using WebApi.Data.DataContext;
using Serilog;
using WebApi.ConsulExtend;

namespace WebApi.Demo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            var redisServerUrl = Configuration.GetConnectionString("RedisServerUrl");

            //����jwt
            //��ȡappsettings.json�ļ���������֤����Կ��Secret�������ڣ�Aud����Ϣ
            var audienceConfig = Configuration.GetSection("Audience");
            //��ȡ��ȫ��Կ
            var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(audienceConfig["Secret"]));
            //tokenҪ��֤�Ĳ�������
            var tokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,//������֤��ȫ��Կ
                IssuerSigningKey = signingKey,//��ֵ��ȫ��Կ
                ValidateIssuer = true,//������֤ǩ����
                ValidIssuer = audienceConfig["Iss"],//��ֵǩ����
                ValidateAudience = true,//������֤����
                ValidAudience = audienceConfig["Aud"],//��ֵ����
                ValidateLifetime = true,//�Ƿ���֤Token��Ч�ڣ�ʹ�õ�ǰʱ����Token��Claims�е�NotBefore��Expires�Ա�
                ClockSkew = TimeSpan.Zero,//����ķ�����ʱ��ƫ����
                RequireExpirationTime = true,//�Ƿ�Ҫ��Token��Claims�б������Expires
            };
            //��ӷ�����֤������ΪTestKey
            services.AddAuthentication(o =>
            {
                o.DefaultScheme = "TestKey";
            })
            .AddJwtBearer("TestKey", x =>
            {
                x.RequireHttpsMetadata = false;
                //��JwtBearerOptions�����У�IssuerSigningKey(ǩ����Կ)��ValidIssuer(Token�䷢����)��ValidAudience(�䷢��˭)���������Ǳ���ġ�
                x.TokenValidationParameters = tokenValidationParameters;
            });

            //���ÿ���
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                {
                    builder.AllowAnyOrigin() //��������Origin����

                           //�����������󷽷���Get,Post,Put,Delete
                           .AllowAnyMethod()

                           //������������ͷ:application/json
                           .AllowAnyHeader();
                });
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("V1", new OpenApiInfo 
                { 
                    Title = "Swagger�ӿ��ĵ�",
                    Version = "V1",
                    Description = "����Web API",
                });
            });

            services.AddScoped<IBookService, BookService>();
            services.AddScoped<IInventoryService, InventoryService>();
            services.AddSingleton<RedisContext>();
            
            #region ����RabbitMQ
            services.Configure<RabbitMQOptions>(Configuration.GetSection("RabbitMQOptions"));
            services.AddSingleton<RabbitMQClient>();
            services.AddSingleton<RabbitMQOptions>();
            services.RegisterService(this.Configuration);
            #endregion

            services.AddDbContext<BMSDbContext>(opt =>
            opt.UseSqlServer(Configuration.GetConnectionString("BMSDB")));

            services.AddLogging(logBuilder => {
                                 logBuilder.ClearProviders();});

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
           

            app.UseHttpsRedirection();
            app.UseSerilogRequestLogging();
            app.UseRouting();
            app.UseCors();

            app.UseAuthentication(); // ��֤
            app.UseAuthorization();  // ��Ȩ

            app.UseSwagger();
            app.UseSwaggerUI(opt =>
            {
                opt.SwaggerEndpoint($"/swagger/V1/swagger.json", "V1");
                opt.RoutePrefix = string.Empty;
            });
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
