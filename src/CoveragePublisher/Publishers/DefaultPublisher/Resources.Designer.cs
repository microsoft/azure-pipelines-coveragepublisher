﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher.Resources" +
                            "", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not rename index.htm to index.html in report directory. Exception: {0}.
        /// </summary>
        internal static string CouldNotRenameExtension {
            get {
                return ResourceManager.GetString("CouldNotRenameExtension", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}- {1} of {2} covered..
        /// </summary>
        internal static string CoveredStats {
            get {
                return ResourceManager.GetString("CoveredStats", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &quot;Directory not found: &apos;{0}&apos;..
        /// </summary>
        internal static string DirectoryNotFound {
            get {
                return ResourceManager.GetString("DirectoryNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} envrionment variable was null or empty..
        /// </summary>
        internal static string EnvVarNullOrEmpty {
            get {
                return ResourceManager.GetString("EnvVarNullOrEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error occurred while publishing code coverage files. Error: {0}..
        /// </summary>
        internal static string ErrorOccurredWhilePublishingCCFiles {
            get {
                return ResourceManager.GetString("ErrorOccurredWhilePublishingCCFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to get FF {0} Value. By default, publishing data to TCM..
        /// </summary>
        internal static string FailedToGetFeatureFlag {
            get {
                return ResourceManager.GetString("FailedToGetFeatureFlag", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to upload coverage summary. Exception: {0}..
        /// </summary>
        internal static string FailedtoUploadCoverageSummary {
            get {
                return ResourceManager.GetString("FailedtoUploadCoverageSummary", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to upload file coverage data. Exception: {1}.
        /// </summary>
        internal static string FailedToUploadFileCoverage {
            get {
                return ResourceManager.GetString("FailedToUploadFileCoverage", resourceCulture);
            }
        }

		/// <summary>
		///   Looks up a localized string similar to Failed to upload file coverage data. Exception: {1}.
		/// </summary>
		internal static string FailedToUploadNativeCoverageFiles
		{
			get
			{
				return ResourceManager.GetString("FailedToUploadNativeCoverageFiles", resourceCulture);
			}
		}

		/// <summary>
		///   Looks up a localized string similar to Unable to copy file to server StatusCode={0}: {1}. Source file path: {2}. Target server path: {3}..
		/// </summary>
		internal static string FileContainerUploadFailed {
            get {
                return ResourceManager.GetString("FileContainerUploadFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File upload has been cancelled during upload file: &apos;{0}&apos;..
        /// </summary>
        internal static string FileUploadCancelled {
            get {
                return ResourceManager.GetString("FileUploadCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Detail upload trace for file that fail to upload: {0}..
        /// </summary>
        internal static string FileUploadDetailTrace {
            get {
                return ResourceManager.GetString("FileUploadDetailTrace", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fail to upload &apos;{0}&apos; due to &apos;{1}&apos;..
        /// </summary>
        internal static string FileUploadFailed {
            get {
                return ResourceManager.GetString("FileUploadFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File upload failed even after retry..
        /// </summary>
        internal static string FileUploadFailedAfterRetry {
            get {
                return ResourceManager.GetString("FileUploadFailedAfterRetry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} files failed to upload, retry these files after a minute..
        /// </summary>
        internal static string FileUploadFailedRetryLater {
            get {
                return ResourceManager.GetString("FileUploadFailedRetryLater", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File error &apos;{0}&apos; when uploading file &apos;{1}&apos;..
        /// </summary>
        internal static string FileUploadFileOpenFailed {
            get {
                return ResourceManager.GetString("FileUploadFileOpenFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File: &apos;{0}&apos; took {1} milliseconds to finish upload..
        /// </summary>
        internal static string FileUploadFinish {
            get {
                return ResourceManager.GetString("FileUploadFinish", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Total file: {0} ---- Processed file: {1} ({2}%)..
        /// </summary>
        internal static string FileUploadProgress {
            get {
                return ResourceManager.GetString("FileUploadProgress", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Uploading &apos;{0}&apos; ({1}%)..
        /// </summary>
        internal static string FileUploadProgressDetail {
            get {
                return ResourceManager.GetString("FileUploadProgressDetail", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Start retry {0} failed files upload..
        /// </summary>
        internal static string FileUploadRetry {
            get {
                return ResourceManager.GetString("FileUploadRetry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Retry file upload after {0} seconds..
        /// </summary>
        internal static string FileUploadRetryInSecond {
            get {
                return ResourceManager.GetString("FileUploadRetryInSecond", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File upload succeed after retry..
        /// </summary>
        internal static string FileUploadRetrySucceed {
            get {
                return ResourceManager.GetString("FileUploadRetrySucceed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File upload succeed..
        /// </summary>
        internal static string FileUploadSucceed {
            get {
                return ResourceManager.GetString("FileUploadSucceed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Published &apos;{0}&apos; as artifact &apos;{1}&apos;..
        /// </summary>
        internal static string PublishedCodeCoverageArtifact {
            get {
                return ResourceManager.GetString("PublishedCodeCoverageArtifact", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Publishing code coverage HTML report..
        /// </summary>
        internal static string PublishingCodeCoverageReport {
            get {
                return ResourceManager.GetString("PublishingCodeCoverageReport", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Publishing coverage summary data..
        /// </summary>
        internal static string PublishingCodeCoverageSummary {
            get {
                return ResourceManager.GetString("PublishingCodeCoverageSummary", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Publishing file coverage data..
        /// </summary>
        internal static string PublishingFileCoverage {
            get {
                return ResourceManager.GetString("PublishingFileCoverage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Renaming &apos;{0}&apos; to &apos;{1}&apos; failed while publishing code coverage files. Inner Exception: &apos;{2}&apos;..
        /// </summary>
        internal static string RenameIndexFileCoberturaFailed {
            get {
                return ResourceManager.GetString("RenameIndexFileCoberturaFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Uploading {0} files..
        /// </summary>
        internal static string TotalUploadFiles {
            get {
                return ResourceManager.GetString("TotalUploadFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Artifact upload completed: {0} succeeded, {1} failed.
        /// </summary>
        internal static string ArtifactUploadCompleted {
            get {
                return ResourceManager.GetString("ArtifactUploadCompleted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Processing batch {0}: files {1}-{2} of {3}.
        /// </summary>
        internal static string ProcessingBatch {
            get {
                return ResourceManager.GetString("ProcessingBatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error in upload: {0}.
        /// </summary>
        internal static string ErrorInUpload {
            get {
                return ResourceManager.GetString("ErrorInUpload", resourceCulture);
            }
        }
    }
}
