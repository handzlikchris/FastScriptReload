using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveVRTools.Runtime.Common;
using ImmersiveVrToolsCommon.Runtime.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace FastScriptReload.Editor.Compilation
{
    public class ChatGptApi
    {
        private const string ChatEndpoint = "https://api.openai.com/v1/chat/completions";
        
        public static ChatCompletionResponse SendChatRequest(string prompt, string apiKey, UnityMainThreadDispatcher mainThreadDispatcher, Action progressUpdateFn, 
            string model = "gpt-3.5-turbo", float triggerProgressUpdateEveryNSeconds = 3)
        {
            return Task.Run(() =>
            {
                var payload = new ChatRequestPayload
                {
                    model = model,
                    temperature = 0.1f,
                    messages = new[]
                    {
                        new ChatMessage()
                        {
                            content = prompt,
                            role = "user"
                        }
                    }
                };
                var requestBody = JsonUtility.ToJson(payload);
                var requestEncoding = Encoding.UTF8.GetBytes(requestBody);
                
                var isRequestDone = false;
                ChatCompletionResponse response = null;
                string errorText = null;
                mainThreadDispatcher.Enqueue(() =>
                {
                    var request = UnityWebRequest.Post(ChatEndpoint, "POST");
                    request.uploadHandler = new UploadHandlerRaw(requestEncoding);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SendWebRequest();

                    mainThreadDispatcher.StartCoroutine(WaitTillDone(request, () =>
                    {
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            errorText = request.error;
                        }
                        else
                        {
                            response = JsonUtility.FromJson<ChatCompletionResponse>(request.downloadHandler.text);
                        }
                    }, progressUpdateFn, triggerProgressUpdateEveryNSeconds));
                });

                while (response == null && errorText == null) //can't poll .isDone outside of main thread
                {
                    Thread.Sleep(100);
                }

                if (!string.IsNullOrEmpty(errorText))
                {
                    throw new Exception(errorText);
                }
                
                return response;
            }).Result;
        }
        
        private static IEnumerator WaitTillDone(UnityWebRequest request, Action action, Action progressUpdate, float triggerProgressUpdateEveryNSeconds)
        {
            var runningSinceLastProgressUpdate = 0f;
            
            while (!request.isDone)
            {
                if (runningSinceLastProgressUpdate > triggerProgressUpdateEveryNSeconds)
                {
                    try
                    {
                        progressUpdate();
                    }
                    catch (Exception e)
                    {
                        LoggerScoped.LogWarning($"Error while running progress update for {nameof(ChatGptErrorAwareSourceCodeAdjuster)}");
                    }
                    runningSinceLastProgressUpdate = 0;
                }
                
                runningSinceLastProgressUpdate += Time.deltaTime;
                yield return null;
            }
            
            action();
        }
        
        
        [Serializable]
        public class ChatRequestPayload
        {
            public string model;
            public float temperature;
            public ChatMessage[] messages;
        }

        [Serializable]
        public class ChatMessage
        {
            public string content;
            public string role;
        }
    
        [Serializable]
        public class ChatCompletionResponse
        {
            public string id;
            public string @object;
            public long created;
            public string model;
            public Usage usage;
            public Choice[] choices;
        }

        [Serializable]
        public class Usage
        {
            public int promptTokens;
            public int completionTokens;
            public int totalTokens;
        }

        [Serializable]
        public class Choice
        {
            public Message message;
            public string finishReason;
            public int index;
        }

        [Serializable]
        public class Message
        {
            public string role;
            public string content;
        }
    }
    
    public class ChatGptErrorAwareSourceCodeAdjuster : ISourceCodeAdjuster
    {
        private static string FixErrorsPromptTemplate = 
@"Unity C# code fails to compile with errors, please fix the code to make it compilable.
Do not remove __Patched_ postfix from class names, instead adjust the code accordingly.
Do not include any explanation in your response, only code.

Error Messages:
<errorMessage>

Code:
<code>";
        
        private readonly string _latestCompilationError;
        private readonly List<string> _typeNamesDefinitions;
        private readonly string _chatGptApiKey;
        private readonly UnityMainThreadDispatcher _unityMainThreadDispatcher;

        public ChatGptErrorAwareSourceCodeAdjuster(string latestCompilationError, List<string> typeNamesDefinitions, string chatGptApiKey, UnityMainThreadDispatcher unityMainThreadDispatcher)
        {
            _latestCompilationError = latestCompilationError;
            _typeNamesDefinitions = typeNamesDefinitions;
            _chatGptApiKey = chatGptApiKey;
            _unityMainThreadDispatcher = unityMainThreadDispatcher;
        }

        public CreateSourceCodeCombinedContentsResult CreateSourceCodeCombinedContents(List<string> sourceCodeFiles, List<string> definedPreprocessorSymbols)
        {
            var prompt = FixErrorsPromptTemplate
                .Replace("<errorMessage>", _latestCompilationError)
                .Replace("<code>", string.Join(Environment.NewLine, sourceCodeFiles.Select(f => File.ReadAllText(f))));

            LoggerScoped.Log("Attempting to fix compile error via ChatGPT, this may take few moments depending on API load");
            var response = ChatGptApi.SendChatRequest(prompt, _chatGptApiKey, _unityMainThreadDispatcher, () =>
            {
                LoggerScoped.Log("ChatGPT API call still in progress...");
            });
            LoggerScoped.Log("ChatGPT response received");
            
            return new CreateSourceCodeCombinedContentsResult(response.choices.First().message.content, _typeNamesDefinitions);
        }
    }
}