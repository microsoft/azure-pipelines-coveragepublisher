// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Palmmedia.ReportGenerator.Core;
using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    /// <summary>
    /// Gives the report library the lines already read from a safe open.
    /// The library otherwise opens the file name again while drawing the page.
    /// </summary>
    internal static class ApprovedSourcePin
    {
        private static readonly object s_gate = new object();
        private static Type s_readerType;

        internal static bool TryPin(IEnumerable<CodeFile> files, IReadOnlyDictionary<string, string[]> linesByPath)
        {
            if (files == null)
            {
                return false;
            }

            FieldInfo readerField = typeof(CodeFile).GetField(
                "additionalFileReader",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (readerField == null)
            {
                return false;
            }

            object reader;
            try
            {
                reader = CreateReader(linesByPath);
            }
            catch (Exception ex) when (ex is InvalidOperationException
                || ex is TypeLoadException
                || ex is IOException
                || ex is MissingMethodException
                || ex is TargetInvocationException)
            {
                return false;
            }

            if (reader == null)
            {
                return false;
            }

            foreach (CodeFile file in files)
            {
                readerField.SetValue(file, reader);
            }

            return true;
        }

        private static object CreateReader(IReadOnlyDictionary<string, string[]> linesByPath)
        {
            Type readerType = GetOrCreateReaderType();
            var lines = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (linesByPath != null)
            {
                foreach (KeyValuePair<string, string[]> entry in linesByPath)
                {
                    lines[entry.Key] = entry.Value ?? Array.Empty<string>();
                }
            }

            return Activator.CreateInstance(readerType, lines);
        }

        private static Type GetOrCreateReaderType()
        {
            if (s_readerType != null)
            {
                return s_readerType;
            }

            lock (s_gate)
            {
                if (s_readerType != null)
                {
                    return s_readerType;
                }

                System.Reflection.Assembly reportAssembly = typeof(Generator).Assembly;
                Type fileReaderType = reportAssembly.GetType(
                    "Palmmedia.ReportGenerator.Core.Parser.FileReading.IFileReader",
                    throwOnError: true);
                MethodInfo loadFile = fileReaderType.GetMethod("LoadFile");
                if (loadFile == null)
                {
                    throw new InvalidOperationException("The report library does not expose its file reader.");
                }

                var assemblyName = new AssemblyName("ApprovedSourcePinDynamic");
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                ConstructorInfo attributeConstructor = typeof(IgnoresAccessChecksToAttribute)
                    .GetConstructor(new[] { typeof(string) });
                assembly.SetCustomAttribute(new CustomAttributeBuilder(
                    attributeConstructor,
                    new object[] { reportAssembly.GetName().Name }));

                ModuleBuilder module = assembly.DefineDynamicModule("ApprovedSourcePinDynamic");
                TypeBuilder type = module.DefineType(
                    "PinnedFileReader",
                    TypeAttributes.Public | TypeAttributes.Class);
                type.AddInterfaceImplementation(fileReaderType);

                FieldBuilder linesField = type.DefineField(
                    "_lines",
                    typeof(Dictionary<string, string[]>),
                    FieldAttributes.Private);
                ConstructorBuilder constructor = type.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    new[] { typeof(Dictionary<string, string[]>) });
                ILGenerator constructorIl = constructor.GetILGenerator();
                constructorIl.Emit(OpCodes.Ldarg_0);
                constructorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
                constructorIl.Emit(OpCodes.Ldarg_0);
                constructorIl.Emit(OpCodes.Ldarg_1);
                constructorIl.Emit(OpCodes.Stfld, linesField);
                constructorIl.Emit(OpCodes.Ret);

                MethodBuilder method = type.DefineMethod(
                    "LoadFile",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(string[]),
                    new[] { typeof(string), typeof(string).MakeByRefType() });
                type.DefineMethodOverride(method, loadFile);
                ILGenerator il = method.GetILGenerator();
                LocalBuilder linesLocal = il.DeclareLocal(typeof(string[]));
                Label found = il.DefineLabel();
                MethodInfo tryGetValue = typeof(Dictionary<string, string[]>).GetMethod(
                    "TryGetValue",
                    new[] { typeof(string), typeof(string[]).MakeByRefType() });

                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Stind_Ref);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, linesField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldloca, linesLocal);
                il.Emit(OpCodes.Callvirt, tryGetValue);
                il.Emit(OpCodes.Brtrue, found);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Newarr, typeof(string));
                il.Emit(OpCodes.Ret);
                il.MarkLabel(found);
                il.Emit(OpCodes.Ldloc, linesLocal);
                il.Emit(OpCodes.Ret);

                s_readerType = type.CreateType();
                return s_readerType;
            }
        }
    }
}
