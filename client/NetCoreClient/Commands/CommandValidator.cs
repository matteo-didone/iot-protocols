using System.Text.Json;

namespace NetCoreClient.Commands
{
    public class CommandValidator
    {
        public static event Action<string, bool>? PowerStateChanged;

        public static void HandleCommand(string topic, string payload)
        {
            Console.WriteLine("[DEBUG] Received command:");
            Console.WriteLine($"Topic: {topic}");
            Console.WriteLine($"Payload: {payload}");

            try
            {
                using JsonDocument document = JsonDocument.Parse(payload);
                JsonElement root = document.RootElement;

                Console.WriteLine("[DEBUG] JSON parsed successfully.");

                if (ValidateCommandFormat(root, out string validationError))
                {
                    Console.WriteLine("[DEBUG] Command validation successful.");
                    ProcessValidCommand(topic, root);
                }
                else
                {
                    Console.WriteLine($"[ERROR] Command validation failed: {validationError}");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ERROR] JSON parsing error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error in command processing: {ex.Message}");
            }
        }

        private static bool ValidateCommandFormat(JsonElement command, out string error)
        {
            error = string.Empty;

            if (!command.TryGetProperty("action", out JsonElement actionElement))
            {
                error = "Missing 'action' field in command";
                return false;
            }

            string action = actionElement.GetString()?.ToLower() ?? "unknown";

            switch (action)
            {
                case "power":
                case "night_light":
                    if (!command.TryGetProperty("state", out JsonElement stateElement) || stateElement.ValueKind != JsonValueKind.True && stateElement.ValueKind != JsonValueKind.False)
                    {
                        error = $"Invalid 'state' field for command {action}. It must be a boolean.";
                        return false;
                    }
                    break;

                case "maintenance":
                    if (!command.TryGetProperty("enabled", out JsonElement enabledElement) || enabledElement.ValueKind != JsonValueKind.True && enabledElement.ValueKind != JsonValueKind.False)
                    {
                        error = "Invalid 'enabled' field for maintenance command. It must be a boolean.";
                        return false;
                    }
                    break;

                default:
                    error = $"Unrecognized command '{action}'.";
                    return false;
            }

            return true;
        }

        private static void ProcessValidCommand(string topic, JsonElement command)
        {
            string action = command.GetProperty("action").GetString()?.ToLower() ?? "unknown";
            string coolerId = topic.Split('/')[1];

            switch (action)
            {
                case "power":
                    bool powerState = command.GetProperty("state").GetBoolean();
                    Console.WriteLine($"[LOG] Power command received: {(powerState ? "ON" : "OFF")}");
                    PowerStateChanged?.Invoke(coolerId, powerState);
                    break;

                case "night_light":
                    bool lightState = command.GetProperty("state").GetBoolean();
                    Console.WriteLine($"[LOG] Night Light command received: {(lightState ? "ON" : "OFF")}");
                    break;

                case "maintenance":
                    bool maintenanceMode = command.GetProperty("enabled").GetBoolean();
                    Console.WriteLine($"[LOG] Maintenance command received: {(maintenanceMode ? "ENABLED" : "DISABLED")}");
                    break;

                default:
                    Console.WriteLine($"[ERROR] Unhandled action: {action}");
                    break;
            }
        }
    }
}