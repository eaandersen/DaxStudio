﻿using ADOTabular;
using ADOTabular.Interfaces;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.AnalysisServices;
using Newtonsoft.Json;

namespace DaxStudio.UI.ViewModels
{
    

    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class QueryBuilderViewModel : ToolWindowBase
        ,IQueryTextProvider
        ,IHandle<SendColumnToEditorEvent>
        ,IDisposable
        ,ISaveState
    {
        const string NewMeasurePrefix = "MyMeasure";

        [ImportingConstructor]
        public QueryBuilderViewModel(IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions globalOptions)
        {
            EventAggregator = eventAggregator;
            Document = document;
            Options = globalOptions;
            Filters = new QueryBuilderFilterList(GetModelCapabilities);
            Title = "Builder";
            DefaultDockingPane = "DockMidLeft";
            IsVisible = false;
            Columns = new QueryBuilderFieldList(EventAggregator);
            Columns.PropertyChanged += OnColumnsPropertyChanged;
            OrderBy = new QueryBuilderFieldList(EventAggregator);
            VisibilityChanged += OnVisibilityChanged;
        }

        private void OnVisibilityChanged(object sender, EventArgs e)
        {
            if (IsVisible) EventAggregator.Subscribe(this);
            else EventAggregator.Unsubscribe(this);
        }

        private void OnColumnsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyOfPropertyChange(nameof(CanRunQuery));
            NotifyOfPropertyChange(nameof(CanSendTextToEditor));
            NotifyOfPropertyChange(nameof(CanOrderBy));
        }


        // ReSharper disable once UnusedMember.Global
        public new bool CanHide => true;
        public bool CanOrderBy => Columns.Any();
        [JsonProperty]
        public QueryBuilderFieldList Columns { get; } 
        [JsonProperty]
        public QueryBuilderFilterList Filters { get; }
        [JsonProperty]
        public QueryBuilderFieldList OrderBy { get; }

        private bool _isEnabled = true;
        public new bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (_isEnabled) EventAggregator.Subscribe(this);
                else EventAggregator.Unsubscribe(this);
                NotifyOfPropertyChange();
            }
        }
        public IEventAggregator EventAggregator { get; }
        public DocumentViewModel Document { get; }
        public IGlobalOptions Options { get; }

        private QueryBuilderColumn _selectedColumn;
        public QueryBuilderColumn SelectedColumn { get => _selectedColumn;
            set {
                _selectedColumn = value;
                NotifyOfPropertyChange();
            }
        }

        private int _selectedIndex;
        public int SelectedIndex { get => _selectedIndex;
            set {
                _selectedIndex = value;
                NotifyOfPropertyChange();
            }
        }

        public string QueryText { 
            get { 
                try {
                    var modelCaps = GetModelCapabilities();
                    return QueryBuilder.BuildQuery(modelCaps,Columns.Items, Filters.Items,OrderBy.Items); 
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(QueryBuilderViewModel), nameof(QueryText), ex.Message);
                    EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error generating query: {ex.Message}"));
                }
                return string.Empty;
            } 
        
        }

        private IModelCapabilities GetModelCapabilities()
        {
            var model = Document.Connection.SelectedModel;
            return model.Capabilities;
        }

        public Dictionary<string, QueryParameter> QueryParameters
        {
            get;
        } = new Dictionary<string, QueryParameter>();

        // ReSharper disable once UnusedMember.Global
        public void RunQuery() {
            if (! CheckForCrossjoins() )
                EventAggregator.PublishOnUIThread(new RunQueryEvent(Document.SelectedTarget) { QueryProvider = this });
        }

        private bool CheckForCrossjoins()
        {
            bool hasMeasures = this.Columns.Any(c => c.ObjectType == ADOTabularObjectType.Measure);
            if (hasMeasures) return false;  // we have a measure so that should prevent a large crossjoin
            
            var cols = this.Columns.GroupBy(c => c.TableName);
            if (cols.Count() == 1) return false;  // if all the columns are from one table it will not produce a crossjoin

            return MessageBox.Show("Including columns from multiple tables without a measure is likely to result in a large crossjoin which could use a lot of memory.\n\nAre you sure you want to proceed?", "Potential Crossjoin Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No;
        }

        // ReSharper disable once UnusedMember.Global
        public void SendTextToEditor()
        {
            EventAggregator.PublishOnUIThread(new SendTextToEditor(QueryText));
        }

        public bool CanRunQuery => Columns.Items.Count > 0;

        public bool CanSendTextToEditor => Columns.Items.Count > 0;

        // ReSharper disable once UnusedMember.Global
        public void AddNewMeasure()
        {
            var firstTable = Document.Connection.SelectedModel.Tables.First();
            // TODO - need to make sure key is unique
            var newMeasureName = GetCustomMeasureName();
            var newMeasure = new QueryBuilderColumn(newMeasureName,firstTable);
            Columns.Add(newMeasure);
            //newMeasure.IsModelItem = false;
            SelectedColumn = newMeasure;
            SelectedIndex = Columns.Count - 1;
            Columns.EditMeasure(newMeasure);
            IsEnabled = false;
            //EventAggregator.PublishOnUIThread(new ShowMeasureExpressionEditor(newMeasure));
        }

        // Finds a unique name for the new measure
        public string GetCustomMeasureName()
        {
            int customMeasureCnt = Columns.Count(c => c.Caption.StartsWith(NewMeasurePrefix));
            if (customMeasureCnt == 0) return NewMeasurePrefix;
            // if the user has deleted some earlier custom measure numbers we need to loop and keep
            // searching until we find an unused one
            while (Columns.Any(c => c.Caption == $"{NewMeasurePrefix}{customMeasureCnt}" ))
            {
                customMeasureCnt++;
            }
            return $"{NewMeasurePrefix}{customMeasureCnt}";

        }

        protected override void OnVisibilityChanged(EventArgs e)
        {
            base.OnVisibilityChanged(e);
            
        }

        #region IDisposable Support
        private bool _disposedValue; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // unhook PropertyChanged event
                    Columns.PropertyChanged -= OnColumnsPropertyChanged;
                }

                _disposedValue = true;
            }
        }

        
        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }


        #endregion

        public void Handle(SendColumnToEditorEvent message)
        {
            if (message.IsFilter) AddColumnToFilters(message.Column);
            else AddColumnToColumns(message.Column);
        }

        private void AddColumnToColumns(ITreeviewColumn column)
        {
            if (Columns.Contains(column.InternalColumn))
            {
                // write warning and return
            }
            Columns.Add(column.InternalColumn);
        }

        private void AddColumnToFilters(ITreeviewColumn column)
        {
            Filters.Add(column.InternalColumn);
        }

        public void Save(string filename)
        {
            string json = JsonConvert.SerializeObject(this);
            File.WriteAllText(filename + ".queryBuilder", json);
        }

        public void Load(string filename)
        {
            filename = filename + ".queryBuilder";
            if (!File.Exists(filename)) return;

            // TODO - show query builder
            //EventAggregator.PublishOnUIThread(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            var model = JsonConvert.DeserializeObject<QueryBuilderViewModel>(data);
        }
    }
}
