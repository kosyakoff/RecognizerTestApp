﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Recognizer.Core.Resources.Strings {
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
    public class CommonResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal CommonResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Recognizer.Core.Resources.Strings.CommonResources", typeof(CommonResources).Assembly);
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
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Recognizer.
        /// </summary>
        public static string app_name {
            get {
                return ResourceManager.GetString("app_name", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Нужна камера.
        /// </summary>
        public static string camera_request_permission {
            get {
                return ResourceManager.GetString("camera_request_permission", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Качество.
        /// </summary>
        public static string common_quality {
            get {
                return ResourceManager.GetString("common_quality", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Нужен интернет.
        /// </summary>
        public static string internet_request_permission {
            get {
                return ResourceManager.GetString("internet_request_permission", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Камера отсутсвует.
        /// </summary>
        public static string no_camera {
            get {
                return ResourceManager.GetString("no_camera", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to На клиенте.
        /// </summary>
        public static string on_client {
            get {
                return ResourceManager.GetString("on_client", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to На сервере.
        /// </summary>
        public static string on_server {
            get {
                return ResourceManager.GetString("on_server", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Перезапустить.
        /// </summary>
        public static string rerun {
            get {
                return ResourceManager.GetString("rerun", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Нужна запись.
        /// </summary>
        public static string write_request_permission {
            get {
                return ResourceManager.GetString("write_request_permission", resourceCulture);
            }
        }
    }
}
