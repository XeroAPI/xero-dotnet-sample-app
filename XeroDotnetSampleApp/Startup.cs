using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xero.NetStandard.OAuth2.Config;
using Microsoft.EntityFrameworkCore;

using XeroDotnetSampleApp.Services;

namespace XeroDotnetSampleApp
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
            services.Configure<XeroConfiguration>(Configuration.GetSection("XeroConfiguration"));
            services.Configure<SignUpWithXeroSettings>(Configuration.GetSection("SignUpWithXeroSettings"));
            services.Configure<XeroAppStoreSubscriptionSettings>(Configuration.GetSection("XeroAppStoreSubscriptionSettings"));
            services.Configure<WebhookSettings>(Configuration.GetSection("WebhookSettings"));
            services.Configure<DatabaseConfiguration>(Configuration.GetSection("DatabaseConfiguration"));

            services.AddHttpClient();
            services.AddSession();
            services.AddMvc(options => options.EnableEndpointRouting = false);
            
            services.AddDistributedMemoryCache();
            
            // Configure DbContext to use SQLite
            services.AddDbContext<UserContext>(options =>
                options.UseSqlite(Configuration.GetSection("DatabaseConfiguration").GetValue<string>("DatabaseConnectionString")));

            // Add services as scoped services
            services.AddScoped<AppStoreService>();
            services.AddScoped<DatabaseService>();

            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();
            app.UseSession();

            using (var scope = app.ApplicationServices.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<UserContext>();
                dbContext.Database.Migrate();
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
