using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IoTDashboard
{
    /// <summary>
    /// Sends IoT component data to backend API via HTTP
    /// WebGL-compatible using UnityWebRequest
    /// </summary>
    public class WebGLDataSender : MonoBehaviour
    {
        [Header("API Settings")]
        [Tooltip("Backend API base URL")]
        public string ApiBaseUrl = "http://localhost:3000";
        
        [Header("Retry Settings")]
        [Tooltip("Number of retry attempts on failure")]
        public int MaxRetries = 3;
        
        [Tooltip("Delay between retries in seconds")]
        public float RetryDelay = 1.0f;
        
        private Queue<List<IoTComponentData>> pendingDataQueue = new Queue<List<IoTComponentData>>();
        private bool isSending = false;
        private int consecutiveFailures = 0;
        
        /// <summary>
        /// Sends component data to backend API
        /// </summary>
        public void SendComponentData(List<IoTComponentData> components)
        {
            if (components == null || components.Count == 0)
                return;
            
            // If already sending, queue the data
            if (isSending)
            {
                pendingDataQueue.Enqueue(new List<IoTComponentData>(components));
                return;
            }
            
            StartCoroutine(SendDataCoroutine(components));
        }
        
        private IEnumerator SendDataCoroutine(List<IoTComponentData> components)
        {
            isSending = true;
            int attempt = 0;
            bool success = false;
            
            while (attempt < MaxRetries && !success)
            {
                string jsonData = SerializeComponents(components);
                string url = ApiBaseUrl + "/api/components";
                
                using (UnityEngine.Networking.UnityWebRequest request = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                    request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    
                    yield return request.SendWebRequest();
                    
                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        success = true;
                        consecutiveFailures = 0;
                        
                        if (request.responseCode == 200)
                        {
                            Debug.Log($"[WebGLDataSender] Successfully sent {components.Count} components to API");
                        }
                    }
                    else
                    {
                        attempt++;
                        consecutiveFailures++;
                        
                        if (attempt < MaxRetries)
                        {
                            Debug.LogWarning($"[WebGLDataSender] Send failed (attempt {attempt}/{MaxRetries}): {request.error}. Retrying...");
                            yield return new WaitForSeconds(RetryDelay);
                        }
                        else
                        {
                            Debug.LogError($"[WebGLDataSender] Failed to send data after {MaxRetries} attempts: {request.error}");
                        }
                    }
                }
            }
            
            isSending = false;
            
            // Process queued data if any
            if (pendingDataQueue.Count > 0)
            {
                var nextData = pendingDataQueue.Dequeue();
                StartCoroutine(SendDataCoroutine(nextData));
            }
        }
        
        /// <summary>
        /// Serializes component data to JSON
        /// </summary>
        private string SerializeComponents(List<IoTComponentData> components)
        {
            System.Text.StringBuilder json = new System.Text.StringBuilder();
            json.Append("{\"components\":[");
            
            bool first = true;
            foreach (var comp in components)
            {
                if (!first) json.Append(",");
                first = false;
                
                json.Append("{");
                json.Append($"\"name\":\"{EscapeJson(comp.ComponentName)}\",");
                json.Append($"\"type\":\"{EscapeJson(comp.ComponentType)}\",");
                json.Append($"\"active\":{comp.IsActive.ToString().ToLower()},");
                json.Append($"\"status\":\"{EscapeJson(comp.Status)}\",");
                json.Append($"\"value\":{comp.Value},");
                json.Append($"\"unit\":\"{EscapeJson(comp.Unit)}\",");
                json.Append($"\"color\":\"{ColorToHex(comp.StatusColor)}\",");
                json.Append($"\"category\":\"{EscapeJson(comp.Category ?? "other")}\",");
                json.Append($"\"hasTransportSurface\":{comp.HasTransportSurface.ToString().ToLower()},");
                json.Append($"\"parentRobot\":{(string.IsNullOrEmpty(comp.ParentRobot) ? "null" : "\"" + EscapeJson(comp.ParentRobot) + "\"")},");
                json.Append($"\"isRobotAxis\":{comp.IsRobotAxis.ToString().ToLower()},");
                json.Append($"\"isRobotGrip\":{comp.IsRobotGrip.ToString().ToLower()},");
                json.Append($"\"timestamp\":\"{System.DateTime.UtcNow.ToString("o")}\"");
                
                if (comp.Metadata != null && comp.Metadata.Count > 0)
                {
                    json.Append(",\"metadata\":{");
                    bool firstMeta = true;
                    foreach (var kvp in comp.Metadata)
                    {
                        if (!firstMeta) json.Append(",");
                        firstMeta = false;
                        json.Append($"\"{EscapeJson(kvp.Key)}\":{SerializeMetadataValue(kvp.Value)}");
                    }
                    json.Append("}");
                }
                json.Append("}");
            }
            
            json.Append("]}");
            return json.ToString();
        }
        
        private string ColorToHex(Color color)
        {
            int r = Mathf.RoundToInt(color.r * 255);
            int g = Mathf.RoundToInt(color.g * 255);
            int b = Mathf.RoundToInt(color.b * 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        
        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
        
        private string SerializeMetadataValue(object value)
        {
            if (value == null)
            {
                return "null";
            }
            
            switch (value)
            {
                case bool boolVal:
                    return boolVal.ToString().ToLower();
                case int intVal:
                    return intVal.ToString();
                case float floatVal:
                    return floatVal.ToString(CultureInfo.InvariantCulture);
                case double doubleVal:
                    return doubleVal.ToString(CultureInfo.InvariantCulture);
                case string strVal:
                    return $"\"{EscapeJson(strVal)}\"";
                default:
                    return $"\"{EscapeJson(value.ToString())}\"";
            }
        }
    }
}






