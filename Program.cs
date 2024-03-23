using Microsoft.Playwright;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO; // Added for File access
using System.Linq; // Added for Where, ToArray LINQ methods
using System.Collections.Generic;
using Json = System.Text.Json.JsonSerializer; // Alias para JsonSerializer de System.Text.Json

namespace TwF
{
    class Program
    {
        
        static async Task Main(string[] args)
        {
            // Prueba con un solo token para simplificar
            await ProcessToken("xxxxzs8xxxxx4x9bxoxxxxx7gn5n");
            Console.WriteLine("Token procesado. Presiona cualquier tecla para salir.");
            Console.ReadLine();
        }


        public static string[] ParseTokens(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var lines = content.Trim().Split('\n');
                return lines.Where(line => !string.IsNullOrEmpty(line)).ToArray();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading the file {filePath}: {ex.Message}");
                return new string[0];
            }
        }

        public static async Task ProcessTokensFromFile(string token)
        {
            // var tokens = TokenParser.ParseTokens(filePath);

            // foreach (var token in tokens)
            // {

            await ProcessToken(token);
            // }
        }


        public static async Task ProcessToken(string token)
        {
            // Inicializar Playwright
            var playwright = await Playwright.CreateAsync();
            
            // Seleccionar el navegador. Ejemplo: Chromium
            var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true // Cambiar a true para no mostrar la interfaz gráfica del navegador
            });

            // Crear una nueva página (pestaña)
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var expiresTimestamp = 1711134459;
            // Convertir el timestamp UNIX a DateTime
            var expiresDateTime = DateTimeOffset.FromUnixTimeSeconds(expiresTimestamp).DateTime;

            var cookies = new[]
            {
                new Cookie
                {
                    Name = "auth-token",
                    Value = token,
                    Domain = ".twitch.tv", // Optional: To apply the cookie to all subdomains, prefix the domain with a dot
                    Path = "/", // Optional: Specify the path where the cookie should be available
                    Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), // Optional: Set an expiration time (Unix timestamp)
                    HttpOnly = false, // Optional: Set to true if the cookie is HTTP-only
                    Secure = true, // Optional: Set to true if the cookie should only be sent over HTTPS
                    SameSite = SameSiteAttribute.Lax // Optional: Set the sameSite attribute (Strict, Lax, or None)
                }
            };



            Console.WriteLine("Processing token: " + token);
            await context.ClearCookiesAsync();
            await context.AddCookiesAsync(cookies);

            string Client_ID = null, TokenOAuth = null, Client_Version = null, Device_ID = null, UserAgent = null, TokenIntegrity = null;

            // Definir manejadores de eventos antes de la navegación
            bool requestCaptured = false; // Banderas para controlar el flujo
            bool responseCaptured = false;
            var capturedHeaders = new Dictionary<string, string>();

            page.Request += async (_, request) =>
            {
            if (request.Url.Contains("gql.twitch.tv/integrity"))
            {
                Console.WriteLine($"Intercepted request to {request.Url}");

                    // Suponiendo que 'request' es tu objeto de solicitud HTTP y estas variables están declaradas previamente
                    // Verifica y captura el header 'Authorization' si está presente
                    if (request.Headers.ContainsKey("authorization"))
                    {
                        TokenOAuth = request.Headers["authorization"];
                    }

                    // Verifica y captura el header 'Client-ID' si está presente
                    if (request.Headers.ContainsKey("client-id"))
                    {
                        Client_ID = request.Headers["client-id"];
                    }

                    // Verifica y captura el header 'Client-Version' si está presente
                    if (request.Headers.ContainsKey("client-version"))
                    {
                        Client_Version = request.Headers["client-version"];
                    }

                    // Verifica y captura el header 'User-Agent' si está presente
                    if (request.Headers.ContainsKey("user-agent"))
                    {
                        UserAgent = request.Headers["user-agent"];
                    }

                    // Verifica y captura el header 'X-Device-Id' si está presente
                    if (request.Headers.ContainsKey("x-device-id"))
                    {
                        Device_ID = request.Headers["x-device-id"];
                    }

                    // Imprime los headers capturados
                    Console.WriteLine("Authorization: " + TokenOAuth);
                    Console.WriteLine("Client_ID: " + Client_ID);
                    Console.WriteLine("Client_Version: " + Client_Version);
                    Console.WriteLine("UserAgent: " + UserAgent);
                    Console.WriteLine("Device_ID: " + Device_ID);
                }
            };


            // Define el handler para el evento Response
            page.Response += async (_, response) =>
            {
                try
                {
                    if (response.Url.Contains("gql.twitch.tv/integrity"))
                    {
                        var responseBody = await response.BodyAsync();
                        var responseData = Json.Deserialize<JsonElement>(responseBody);

                        // Suponiendo que el token de integridad está en el cuerpo de la respuesta bajo alguna propiedad
                        if (responseData.TryGetProperty("token", out var tokenProperty))
                        {
                             TokenIntegrity = tokenProperty.GetString();
                            Console.WriteLine($"Token de integridad: {TokenIntegrity}");
                        }
                        else
                        {
                            Console.WriteLine("Token de integridad no encontrado en la respuesta.");
                        }

                        // Añade aquí cualquier otra lógica necesaria para manejar la respuesta
                    }
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("Error al procesar la respuesta: " + error);
                }
            };


            await page.GotoAsync("https://www.twitch.tv/risinget_");
            await page.WaitForTimeoutAsync(1000);


            // Espera a que los eventos sean capturados o hasta un tiempo límite
            var startTime = DateTime.Now;
            while (!(requestCaptured && responseCaptured) && (DateTime.Now - startTime).TotalSeconds < 5)
            {
                await Task.Delay(100); // Espera activa corta para revisar las banderas
            }


            await GraphqlRequest(Client_ID, TokenOAuth, Client_Version, Device_ID, UserAgent, TokenIntegrity);


            await page.WaitForTimeoutAsync(1000);
            await page.CloseAsync();
            await browser.CloseAsync();
        }

        public static async Task GraphqlRequest(string Client_ID, string TokenOAuth, string Client_Version, string Device_ID, string UserAgent, string TokenIntegrity)
        {
            try
            {
                var client = new HttpClient();

                var body = new
                {
                    query = @"
                        mutation FollowButton_FollowUser($input: FollowUserInput!) {
                            followUser(input: $input) {
                                follow {
                                    disableNotifications
                                    user {
                                        id
                                        displayName
                                        login
                                        self {
                                            canFollow
                                            follower {
                                                disableNotifications
                                                followedAt
                                            }
                                        }
                                    }
                                }
                                error {
                                    code
                                }
                            }
                        }
                    ",
                    variables = new
                    {
                        input = new
                        {
                            userId = "516406472",
                            disableNotifications = false,
                            targetID = "516406472",
                        },
                    },
                };

                var jsonBody = JsonConvert.SerializeObject(body);

                // Suponiendo que 'client' es tu HttpClient y las variables están correctamente inicializadas
                if (!string.IsNullOrEmpty(TokenOAuth))
                {
                    client.DefaultRequestHeaders.Add("Authorization", TokenOAuth);
                }

                if (!string.IsNullOrEmpty(Client_ID))
                {
                    client.DefaultRequestHeaders.Add("Client-ID", Client_ID);
                }

                // Asume que TokenIntegrity puede ser null o vacío, entonces verifica antes de agregar
                if (!string.IsNullOrEmpty(TokenIntegrity))
                {
                    client.DefaultRequestHeaders.Add("Client-Integrity", TokenIntegrity);
                }

                if (!string.IsNullOrEmpty(Client_Version))
                {
                    client.DefaultRequestHeaders.Add("Client-Version", Client_Version);
                }

                if (!string.IsNullOrEmpty(UserAgent))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                }

                if (!string.IsNullOrEmpty(Device_ID))
                {
                    client.DefaultRequestHeaders.Add("X-Device-Id", Device_ID);
                }

                var response = await client.PostAsync("https://gql.twitch.tv/gql", new StringContent(jsonBody, Encoding.UTF8, "application/json"));

                var responseString = await response.Content.ReadAsStringAsync();

                // Deserializa la respuesta a un objeto dinámico
                dynamic responseData = JsonConvert.DeserializeObject<dynamic>(responseString);

                // Vuelve a serializar a una cadena JSON con indentación
                var prettyJson = JsonConvert.SerializeObject(responseData, Formatting.Indented);

                Console.WriteLine("Respuesta detallada: " + prettyJson);

                // Inicializa variables para almacenar los valores que buscas
                bool? canFollow = null; // Usa bool? para que pueda tener un valor null
                string followedAt = null;

                // Verifica si la respuesta contiene los datos esperados antes de intentar acceder a ellos
                if (responseData.data != null && responseData.data.followUser != null && responseData.data.followUser.follow != null && responseData.data.followUser.follow.user != null && responseData.data.followUser.follow.user.self != null)
                {
                    canFollow = responseData.data.followUser.follow.user.self.canFollow;
                    followedAt = responseData.data.followUser.follow.user.self.follower.followedAt;
                }

                Console.WriteLine($"Token: {(TokenOAuth != null ? TokenOAuth : "False")} |  IntegrityToken: {(TokenIntegrity != null ? "Yes" : "No")} |  Can Follow: {(canFollow.HasValue ? (canFollow.Value ? "Yes" : "No") : "Not available")} |  Followed At: {followedAt ?? "Not available"}");


            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en la solicitud GraphQL: " + ex.Message);
                Console.WriteLine(JsonConvert.SerializeObject(ex, Formatting.Indented));
            }
        }
    }
}



