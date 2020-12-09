﻿using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ADOTabular.Interfaces;
using Newtonsoft.Json;

namespace DaxStudio.UI.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class QueryBuilderFilter : PropertyChangedBase
    {

        public QueryBuilderFilter(IADOTabularColumn obj, IModelCapabilities modelCapabilities)
        {
            TabularObject = obj;
            ModelCapabilities = modelCapabilities;
        }
        [JsonProperty]
        public IADOTabularColumn TabularObject { get; }
        public IModelCapabilities ModelCapabilities { get; }

        [JsonProperty]
        public string Caption => TabularObject.Caption;

        private FilterType _fitlerType;
        [JsonProperty]
        public FilterType FilterType 
        {  
            get => _fitlerType;
            set {
                _fitlerType = value;
                NotifyOfPropertyChange(nameof(FilterType));
                NotifyOfPropertyChange(nameof(ShowFilterValue));
                NotifyOfPropertyChange(nameof(ShowFilterValue2));
            }
        }

        public IEnumerable<FilterType> FilterTypes
        {
            get
            {
                foreach (FilterType ft in FilterType.GetValues(typeof(FilterType)))
                {
                    switch (ft) {
                        case FilterType.Is:
                        case FilterType.IsNot:
                        case FilterType.IsBlank:
                        case FilterType.IsNotBlank:
                            // the above filters apply to all data types
                            yield return ft;
                            break;
                        case FilterType.StartsWith:
                        case FilterType.DoesNotStartWith:
                        case FilterType.Contains:
                        case FilterType.DoesNotContain:
                            // these filters only apply to strings
                            if (TabularObject.DataType == typeof(string)) yield return ft;
                            break;
                        case FilterType.In:
                        case FilterType.NotIn:
                            // if the data type is string and the model supports TREATAS
                            if (TabularObject.DataType == typeof(string) && ModelCapabilities.DAXFunctions.TreatAs ) yield return ft;
                            break;
                        case FilterType.GreaterThan:
                        case FilterType.GreaterThanOrEqual:
                        case FilterType.LessThan:
                        case FilterType.LessThanOrEqual:
                        case FilterType.Between:
                            // these filters only apply non-strings
                            if (TabularObject.DataType != typeof(string)) yield return ft;
                            break;
                        default:
                            throw new NotSupportedException($"Unknown FilterType '{ft.ToString()}'");

                    }
                }

                //var items = Enum.GetValues(typeof(FilterType)).Cast<FilterType>();
                //return items;
            }
        }

        [JsonProperty]
        public string FilterValue { get; set; }

        public bool ShowFilterValue
        {
            get { return FilterType != FilterType.IsBlank && FilterType != FilterType.IsNotBlank; }
        }
        [JsonProperty]
        public string FilterValue2 { get; set; }
        public bool ShowFilterValue2 => FilterType == FilterType.Between;

    }
}
