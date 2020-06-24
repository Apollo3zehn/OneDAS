﻿using BlazorInputFile;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OneDas.DataManagement.Explorer.Core;
using OneDas.DataManagement.Explorer.ViewModels;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OneDas.DataManagement.Explorer.Shared
{
    public partial class SettingsBox
    {
        #region Constructors

        public SettingsBox()
        {
            this.PropertyChanged = (sender, e) =>
            {
                if (e.PropertyName == nameof(AppStateViewModel.ExportConfiguration))
                {
                    this.InvokeAsync(this.StateHasChanged);
                }
                else if (e.PropertyName == nameof(AppStateViewModel.SelectedDatasets))
                {
                    this.InvokeAsync(this.StateHasChanged);
                }
            };
        }

        #endregion

        #region Properties

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        public bool PresetsDialogIsOpen { get; set; }

        #endregion

        #region Methods

        private void OpenPresetsDialog()
        {
            this.PresetsDialogIsOpen = true;
        }

        private async Task OnSaveExportSettingsAsync()
        {
			var configuration = this.AppState.ExportConfiguration;
			var jsonString = JsonSerializer.Serialize(configuration, new JsonSerializerOptions() { WriteIndented = true });
			await JsInterop.BlobSaveAs(this.JsRuntime, "export.json", Encoding.UTF8.GetBytes(jsonString));
		}

        private async Task OnLoadExportSettingsAsync(IFileListEntry[] files)
        {
            var file = files.FirstOrDefault();

            if (file != null)
            {
                var exportConfiguration = await JsonSerializer.DeserializeAsync<ExportConfiguration>(file.Data);
                this.AppState.SetExportConfiguration(exportConfiguration);
            }
        }

		#endregion
	}
}
