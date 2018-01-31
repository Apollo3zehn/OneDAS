﻿abstract class PluginViewModelBase
{
    public Description: PluginDescriptionViewModel
    public PluginIdentification: PluginIdentificationViewModel
    public IsInSettingsMode: KnockoutObservable<boolean>

    private _model: any

    constructor(pluginSettingsModel: any, pluginIdentification: PluginIdentificationViewModel)
    {
        this._model = pluginSettingsModel
        this.Description = new PluginDescriptionViewModel(pluginSettingsModel.Description)
        this.PluginIdentification = pluginIdentification
        this.IsInSettingsMode = ko.observable<boolean>(false)
    }

    // methods
    public abstract async InitializeAsync(): Promise<any>

    public SendActionRequest = async (instanceId: number, methodName: string, data: any) =>
    {
        return <ActionResponse> await ConnectionManager.InvokeWebClientHub("RequestAction", new ActionRequest(this.Description.Id, instanceId, methodName, data))
    }

    public ExtendModel(model: any)
    {
        //
    }

    public ToModel()
    {
        let model: any = {
            $type: <string>this._model.$type,
            Description: <PluginDescriptionViewModel>this.Description.ToModel()
        }

        this.ExtendModel(model)

        return model
    }

    // commands
    public EnableSettingsMode = () =>
    {
        this.IsInSettingsMode(true)
    }

    public DisableSettingsMode = () =>
    {
        this.IsInSettingsMode(false)
    }

    public ToggleSettingsMode = () =>
    {
        this.IsInSettingsMode(!this.IsInSettingsMode())
    }
}