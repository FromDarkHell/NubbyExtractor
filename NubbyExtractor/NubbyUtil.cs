﻿using AnimatedGif;
using ImageMagick;
using ImageMagick.Colors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Underanalyzer;
using Underanalyzer.Decompiler.AST;
using UndertaleModLib;
using UndertaleModLib.Models;
using UndertaleModLib.Util;

namespace NubbyExtractor
{
    public class NubbyText
    {
        protected string? defaultID { get; }

        protected string? concatenation { get; }

        protected List<dynamic?> arguments { get; }

        public NubbyText(FunctionCallNode functionCallNode)
        {
            if (functionCallNode.FunctionName == "gml_Script_scr_Text")
            {
                defaultID = NubbyUtil.parseVariableNode(functionCallNode.Arguments[0]);

                arguments = functionCallNode.Arguments.Select((x) => NubbyUtil.parseVariableNode(x)).ToList();
                arguments.RemoveAt(0);
            }
            else if (functionCallNode.FunctionName == "string")
            {
                IExpressionNode argumentNode = ((FunctionCallNode)functionCallNode.Arguments[0]).Arguments[0];
                if (argumentNode is BinaryNode)
                {
                    BinaryNode binaryNode = (BinaryNode)argumentNode;
                    if (binaryNode.Instruction.Kind != IGMInstruction.Opcode.Add) throw new NotImplementedException();

                    concatenation = NubbyUtil.parseVariableNode(binaryNode.Right);
                    if (binaryNode.Left is FunctionCallNode)
                    {
                        FunctionCallNode subFunctionCallNode = (FunctionCallNode)binaryNode.Left;
                        if (subFunctionCallNode.ConditionalValue == "gml_Script_scr_Text")
                        {
                            defaultID = NubbyUtil.parseVariableNode(subFunctionCallNode.Arguments[0]);

                            arguments = subFunctionCallNode.Arguments.Select((x) => NubbyUtil.parseVariableNode(x)).ToList();
                            arguments.RemoveAt(0);
                        }
                        else
                        {
                            arguments = [];
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public NubbyText(string? translationKey)
        {
            defaultID = translationKey;
            arguments = [];
        }

        public NubbyText(string[] allArguments)
        {
            defaultID = allArguments[0];
            arguments = [.. allArguments.Skip(1).Cast<dynamic?>()];
        }

        public override string ToString() => ToString(null);

        public string ToString(Dictionary<string, string>? translationMapping = null)
        {
            if (translationMapping == null) return $"scr_Text({defaultID},{concatenation},{string.Join(',', arguments)})";

            string contentBase = defaultID!;
            foreach (var translation in translationMapping)
            {
                if (contentBase == translation.Key)
                {
                    contentBase = translation.Value;
                    break;
                }
            }

            contentBase += (concatenation ?? "");

            if (arguments.Count >= 1)
            {
                int i = 0;
                List<char> letters = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i'];
                foreach (var letter in letters.GetRange(0, arguments.Count))
                {
                    contentBase = contentBase!.Replace($"{{{letter}}}", arguments[i]!.ToString());
                    i++;
                }
            }

            return contentBase;
        }
    }

    public class NubbyTextConverter(Dictionary<string, string>? translationMapping) : JsonConverter<NubbyText>
    {
        protected Dictionary<string, string>? translationMapping = translationMapping;

        public override NubbyText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, NubbyText value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(this.translationMapping));
        }
    }

    public class ImageMagicColorConverter() : JsonConverter<ColorBase>
    {
        public override ColorBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, ColorBase value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    static class NubbyUtil
    {
        public static dynamic? parseVariableNode(IExpressionNode conditionalNode, 
            Dictionary<string, dynamic>? declaredVariables = null,
            UndertaleData? gameData = null)
        {
            if (conditionalNode is Int16Node) return ((Int16Node)conditionalNode).Value;
            if (conditionalNode is Int32Node) return ((Int32Node)conditionalNode).Value;
            if (conditionalNode is DoubleNode) return ((DoubleNode)conditionalNode).Value;

            if (conditionalNode is StringNode)
                return ((StringNode)conditionalNode).Value.Content;

            if (conditionalNode is VariableNode)
            {
                VariableNode variableNode = (VariableNode)conditionalNode;
                if (declaredVariables == null) throw new Exception($"Unable to parse variable node: ${variableNode}");

                if (variableNode.ArrayIndices != null && variableNode.ArrayIndices.Count >= 1)
                {
                    var indexedArray = declaredVariables[variableNode.ConditionalValue];
                    return indexedArray[parseVariableNode(variableNode.ArrayIndices[0])];
                }

                return declaredVariables[variableNode.ConditionalValue];
            }

            if (conditionalNode is BinaryNode)
            {
                BinaryNode binaryNode = (BinaryNode)conditionalNode;
                var leftValue = parseVariableNode(binaryNode.Left, declaredVariables);
                var rightValue = parseVariableNode(binaryNode.Right, declaredVariables);

                switch (binaryNode.Instruction.Kind)
                {
                    case IGMInstruction.Opcode.Add:
                        return leftValue + rightValue;
                    case IGMInstruction.Opcode.Subtract:
                        return leftValue - rightValue;
                    case IGMInstruction.Opcode.Multiply:
                        return leftValue * rightValue;
                    case IGMInstruction.Opcode.Divide:
                        return leftValue / rightValue;
                    case IGMInstruction.Opcode.GMLModulo:
                        return leftValue % rightValue;
                }
            }

            if (conditionalNode is FunctionCallNode)
            {
                FunctionCallNode functionCallNode = (FunctionCallNode)conditionalNode;
                List<dynamic> arguments = [.. functionCallNode.Arguments.Select((x) => parseVariableNode(x, declaredVariables))];

                if (functionCallNode.FunctionName == "gml_Script_scr_Text")
                {
                    if (arguments.Count <= 1 && arguments[0] == "null") return null;
                    if (arguments.Count <= 1) return arguments[0];
                }

                if (functionCallNode.FunctionName == "string")
                    return arguments[0];

                if (functionCallNode.FunctionName == "string_format")
                {
                    var val = (double)arguments[0];

                    int total = (int)arguments[1];
                    int dec = (int)arguments[2];

                    var baseString = val.ToString();
                    return baseString
                        .PadLeft((baseString.Split('.')[0].Length - 1), '0')
                        .PadRight((baseString.Split('.')[1].Length - 1), '0');
                }

                return conditionalNode;
            }

            if (conditionalNode is AssetReferenceNode)
            {
                Debug.Assert(gameData != null);
                var assetReferenceNode = (AssetReferenceNode)conditionalNode;
                switch (assetReferenceNode.AssetType)
                {
                    case AssetType.Object:
                        return gameData.GameObjects[assetReferenceNode.AssetId];
                    case AssetType.Sprite:
                        return gameData.Sprites[assetReferenceNode.AssetId];
                    case AssetType.Sound:
                        return gameData.Sounds[assetReferenceNode.AssetId];
                    case AssetType.Room:
                        return gameData.Rooms[assetReferenceNode.AssetId];
                    case AssetType.Background:
                        return gameData.Backgrounds[assetReferenceNode.AssetId];
                    case AssetType.Path:
                        return gameData.Paths[assetReferenceNode.AssetId];
                    case AssetType.Script:
                        return gameData.Scripts[assetReferenceNode.AssetId];
                    case AssetType.Font:
                        return gameData.Fonts[assetReferenceNode.AssetId];
                    case AssetType.Timeline:
                        return gameData.Timelines[assetReferenceNode.AssetId];
                    case AssetType.Shader:
                        return gameData.Shaders[assetReferenceNode.AssetId];
                    case AssetType.Sequence:
                        return gameData.Sequences[assetReferenceNode.AssetId];
                    case AssetType.AnimCurve:
                        return gameData.AnimationCurves[assetReferenceNode.AssetId];
                    case AssetType.ParticleSystem:
                        return gameData.ParticleSystems[assetReferenceNode.AssetId];
                    case AssetType.RoomInstance:
                        return gameData.Rooms[assetReferenceNode.AssetId];
                }
                
                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }

        public static List<IMagickImage<byte>> loadFramesFromSprite(UndertaleSprite sprite)
        {
            Debug.Assert(sprite != null);

            TextureWorker textureWorker = new TextureWorker();

            List<IMagickImage<byte>> frames = [];
            foreach (var textureEntry in sprite.Textures)
            {
                var pageItem = textureEntry.Texture;
                var embeddedTexture = textureWorker.GetTextureFor(pageItem, sprite.Name.Content, true);

                // Prevents frames with transparent backgrounds from overlapping each other
                embeddedTexture.GifDisposeMethod = GifDisposeMethod.Previous;

                frames.Add(embeddedTexture);
            }

            return frames;
        }
    
        public static ColorRGB integerToRGB(int color)
        {
            byte red = Convert.ToByte((color >> 16) & 0xFF);
            byte green = Convert.ToByte((color >> 8) & 0xFF);
            byte blue = Convert.ToByte((color) & 0xFF);

            return new ColorRGB(red, green, blue);
        }
    }
}
