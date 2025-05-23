using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace NotionCms.Services;

public interface INotionService
{
    Task<List<TeamMember>?> GetTeamMembersAsync();
    Task<List<NotionBlock>> GetPageBlocksAsync(string notionId);
}

public class NotionService(HttpClient httpClient, IConfiguration configuration) : INotionService
{
    public async Task<List<TeamMember>?> GetTeamMembersAsync()
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration["Settings:NotionToken"]);
        var response =
            await httpClient.PostAsync($"v1/databases/{configuration["Settings:NotionDatabaseId"]}/query", null!);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var team = GetTeamMembersFromNotionResponse(content);

        return team;
    }

    public async Task<List<NotionBlock>> GetPageBlocksAsync(string notionBlockPageId)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration["Settings:NotionToken"]);
        var response = await httpClient.GetAsync($"/v1/blocks/{notionBlockPageId}/children");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var blocks = GetBlocksFromNotionResponse(content);

        return blocks;
    }

    private static List<NotionBlock> GetBlocksFromNotionResponse(string content)
    {
        var json = JObject.Parse(content);
        var blocks = new List<NotionBlock>();

        foreach (var block in json["results"]!)
        {
            var type = block["type"]?.ToString() ?? "";
            var contentBlock = block[type];
            var richText = contentBlock?["rich_text"]?.FirstOrDefault()?["plain_text"]?.ToString();
            var language = type == "code" ? contentBlock?["language"]?.ToString() : null;

            string? imageUrl = null;
            if (type == "image")
            {
                var imageType = contentBlock?["type"]?.ToString();
                imageUrl = imageType switch
                {
                    "external" => contentBlock?["external"]?["url"]?.ToString(),
                    "file" => contentBlock?["file"]?["url"]?.ToString(),
                    _ => null
                };
            }

            blocks.Add(new NotionBlock
            {
                Type = type,
                Text = richText,
                Language = language,
                ImageUrl = imageUrl
            });
        }

        return blocks;
    }

    private static List<TeamMember> GetTeamMembersFromNotionResponse(string content)
    {
        var json = JObject.Parse(content);

        var team = new List<TeamMember>();

        foreach (var item in json["results"]!)
        {
            var props = item["properties"];
            var name = props?["Name"]?["title"]?[0]?["plain_text"]?.ToString() ?? "";
            var photo = props?["Photo"]?["files"]?.FirstOrDefault();
            var photoUrl = photo?["file"]?["url"]?.ToString() ?? photo?["external"]?["url"]?.ToString();
            var isDisabled = props?["Disabled"]?["checkbox"]?.ToObject<bool>() ?? false;

            var roleTags = props?["Role"]?["multi_select"] as JArray;
            var roles = new List<Role>();
            if (roleTags != null)
            {
                foreach (var tag in roleTags)
                {
                    var roleName = tag["name"]?.ToString();
                    var role = roleName switch
                    {
                        "Utvikler" => Role.Utvikler,
                        "UX" => Role.UX,
                        "Prosjektledelse" => Role.Prosjektledelse,
                        "Salg" => Role.Salg,
                        "Sjef" => Role.Sjef,
                        _ => Role.Unknown
                    };
                    roles.Add(role);
                }

                team.Add(new TeamMember
                {
                    Name = name,
                    Roles = roles,
                    PhotoUrl = photoUrl,
                    IsDisabled = isDisabled
                });
            }
        }

        return team;
    }
}

public class TeamMember
{
    public string Name { get; set; }
    public List<Role> Roles { get; set; }
    public string PhotoUrl { get; set; }
    public bool IsDisabled { get; set; }
}

public enum Role
{
    Unknown,
    Utvikler,
    UX,
    Prosjektledelse,
    Salg,
    Sjef
}

public class NotionBlock
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public string? Language { get; set; }
    public string? ImageUrl { get; set; }
}
