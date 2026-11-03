using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Elements;
using DocuHelper.Dash.Logic;

namespace DocuHelper.Tools.Dash
{
    internal static class DashItems
    {
        public const string ElementItem = "element";
        public const string SourceItem = "source";
        public const string ParamItem = "param";
        public const string ValueItem = "value";

        public static readonly string[] Kinds = { ElementItem, SourceItem, ParamItem, ValueItem };

        public static string KindOf(ToolInput input)
        {
            var kind = (input.GetOptionalString("kind") ?? ElementItem).Trim();
            if (!Kinds.Contains(kind, StringComparer.Ordinal))
                throw new ToolInputException($"‘kind’는 {string.Join(", ", Kinds)} 중 하나여야 해요.");

            return kind;
        }

        public static JsonObject ObjectOf(ToolInput input, string name)
        {
            var element = input.Get(name);
            if (element.ValueKind != JsonValueKind.Object)
                throw new ToolInputException($"‘{name}’에는 {{ }} 묶음을 주세요.");

            return JsonNode.Parse(element.GetRawText()) as JsonObject ?? new JsonObject();
        }

        public static T Read<T>(JsonObject node) where T : class
        {
            try
            {
                return node.Deserialize<T>(DashFile.Options);
            }
            catch (JsonException e)
            {
                throw new ToolInputException($"내용을 읽지 못했어요. {e.Message}");
            }
        }

        public static JsonObject Write<T>(T value) where T : class => JsonSerializer.SerializeToNode(value, DashFile.Options) as JsonObject ?? new JsonObject();

        public static void Merge(JsonObject target, JsonObject patch)
        {
            foreach (var pair in patch.ToList())
            {
                var key = target.Select(existing => existing.Key).FirstOrDefault(existing => string.Equals(existing, pair.Key, StringComparison.OrdinalIgnoreCase)) ?? pair.Key;
                if (pair.Value == null)
                {
                    target.Remove(key);
                    continue;
                }

                if (pair.Value is JsonObject inner && target[key] is JsonObject existing)
                {
                    Merge(existing, inner);
                    continue;
                }

                target[key] = pair.Value.DeepClone();
            }
        }

        public static List<Element> ChildrenOf(Element element) => element switch
        {
            ContainerElement container => container.Children ??= new List<Element>(),
            RepeatElement repeat => repeat.Template ??= new List<Element>(),
            _ => null,
        };

        public static bool TryLocate(Element root, string id, out Element parent, out int index)
        {
            parent = null;
            index = -1;
            var children = ChildrenOf(root);
            if (children == null)
                return false;

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] == null)
                    continue;

                if (string.Equals(children[i].Id, id, StringComparison.Ordinal))
                {
                    parent = root;
                    index = i;
                    return true;
                }

                if (TryLocate(children[i], id, out parent, out index))
                    return true;
            }

            return false;
        }

        public static bool Contains(Element ancestor, string id)
        {
            if (ancestor == null)
                return false;

            if (string.Equals(ancestor.Id, id, StringComparison.Ordinal))
                return true;

            return ancestor.Nested().Any(child => Contains(child, id));
        }

        public static Element Require(DashDocument document, string id)
        {
            var element = document.Find(id);
            if (element == null)
                throw new ToolInputException($"‘{id}’ 요소를 찾을 수 없어요. 있는 요소: {Ids(document)}");

            return element;
        }

        public static string Ids(DashDocument document) =>
            string.Join(", ", document.Walk().Skip(1).Take(40).Select(element => element.Id + "(" + element.Kind + ")"));

        public static string Problems(DashDocument document)
        {
            var check = document.Check();
            return check.IsValid ? string.Empty : "\nStill to fix: " + string.Join(" / ", check.Problems.Take(8));
        }

        public static string NameOf(JsonObject item) => DashRaw.Text(item, "name")?.Trim();

        public static IList ListOf(DashDocument document, string kind) => kind switch
        {
            SourceItem => document.Sources,
            ParamItem => document.Params,
            _ => document.Values,
        };

        public static int IndexByName(DashDocument document, string kind, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ToolInputException("이름을 알려 주세요.");

            var index = kind switch
            {
                SourceItem => document.Sources.FindIndex(item => string.Equals(item?.Name, name, StringComparison.Ordinal)),
                ParamItem => document.Params.FindIndex(item => string.Equals(item?.Name, name, StringComparison.Ordinal)),
                _ => document.Values.FindIndex(item => string.Equals(item?.Name, name, StringComparison.Ordinal)),
            };
            if (index < 0)
                throw new ToolInputException($"‘{name}’을 찾을 수 없어요.");

            return index;
        }

        public static bool NameTaken(DashDocument document, string kind, string name, int except = -1)
        {
            if (kind == SourceItem)
                return document.Sources.Where((item, at) => at != except).Any(item => string.Equals(item?.Name, name, StringComparison.Ordinal));

            var asParam = kind == ParamItem;
            return document.Params.Where((item, at) => !(asParam && at == except)).Any(item => string.Equals(item?.Name, name, StringComparison.Ordinal))
                || document.Values.Where((item, at) => !(!asParam && at == except)).Any(item => string.Equals(item?.Name, name, StringComparison.Ordinal));
        }

        public static string KindLabel(string kind) => kind switch
        {
            SourceItem => "원본",
            ParamItem => "조절값",
            ValueItem => "값",
            _ => "요소",
        };
    }
}
