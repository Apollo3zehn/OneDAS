﻿abstract class OneDasModuleSelectorViewModelBase
{
    public AllowInputs: KnockoutObservable<boolean>
    public AllowOutputs: KnockoutObservable<boolean>
    public AllowBoolean: KnockoutObservable<boolean>

    public InputSettingsTemplateName: KnockoutObservable<string>
    public OutputSettingsTemplateName: KnockoutObservable<string>

    public InputCount: KnockoutObservable<number>
    public OutputCount: KnockoutObservable<number>

    public InputRemainingBytes: KnockoutObservable<number>
    public OutputRemainingBytes: KnockoutObservable<number>

    public InputRemainingCount: KnockoutObservable<number>
    public OutputRemainingCount: KnockoutObservable<number>

    public SelectedInputDataType: KnockoutObservable<OneDasDataTypeEnum>
    public SelectedOutputDataType: KnockoutObservable<OneDasDataTypeEnum>

    public InputModuleSet: KnockoutObservableArray<OneDasModuleViewModel>
    public OutputModuleSet: KnockoutObservableArray<OneDasModuleViewModel>

    private _onInputModuleSetChanged: EventDispatcher<OneDasModuleSelectorViewModelBase, OneDasModuleViewModel[]>
    private _onOutputModuleSetChanged: EventDispatcher<OneDasModuleSelectorViewModelBase, OneDasModuleViewModel[]>

    constructor(allowInputs: boolean, allowOutputs: boolean, allowBoolean: boolean)
    {
        this.AllowInputs = ko.observable(allowInputs)
        this.AllowOutputs = ko.observable(allowOutputs)
        this.AllowBoolean = ko.observable(allowBoolean)

        this.InputSettingsTemplateName = ko.observable("Project_OneDasModuleSelectorInputSettingsTemplate")
        this.OutputSettingsTemplateName = ko.observable("Project_OneDasModuleSelectorOutputSettingsTemplate")

        this.InputCount = ko.observable<number>(1);
        this.OutputCount = ko.observable<number>(1);

        this.InputRemainingBytes = ko.observable<number>(NaN);
        this.OutputRemainingBytes = ko.observable<number>(NaN);

        this.InputRemainingCount = ko.observable<number>(NaN);
        this.OutputRemainingCount = ko.observable<number>(NaN);

        this.SelectedInputDataType = ko.observable<OneDasDataTypeEnum>(OneDasDataTypeEnum.UINT16);
        this.SelectedOutputDataType = ko.observable<OneDasDataTypeEnum>(OneDasDataTypeEnum.UINT16);

        this.InputModuleSet = ko.observableArray<OneDasModuleViewModel>();
        this.OutputModuleSet = ko.observableArray<OneDasModuleViewModel>();

        this.SelectedInputDataType.subscribe(newValue => { this.Update() })
        this.SelectedOutputDataType.subscribe(newValue => { this.Update() })

        this.InputModuleSet.subscribe(newValue => { this.Update() })
        this.OutputModuleSet.subscribe(newValue => { this.Update() })

        this._onInputModuleSetChanged = new EventDispatcher<OneDasModuleSelectorViewModelBase, OneDasModuleViewModel[]>();
        this._onOutputModuleSetChanged = new EventDispatcher<OneDasModuleSelectorViewModelBase, OneDasModuleViewModel[]>();

        this.Update()
    }

    get OnInputModuleSetChanged(): IEvent<OneDasModuleSelectorViewModelBase, OneDasModuleViewModel[]>
    {
        return this._onInputModuleSetChanged;
    }

    get OnOutputModuleSetChanged(): IEvent<OneDasModuleSelectorViewModelBase, OneDasModuleViewModel[]>
    {
        return this._onOutputModuleSetChanged;
    }

    // methods
    public abstract Update(): void

    public CreateInputModule()
    {
        return new OneDasModuleViewModel(new OneDasModuleModel(this.SelectedInputDataType(), DataDirectionEnum.Input, this.InputCount()))
    }

    public CreateOutputModule()
    {
        return new OneDasModuleViewModel(new OneDasModuleModel(this.SelectedOutputDataType(), DataDirectionEnum.Output, this.OutputCount()))
    }

    // commands
    public AddInputModule = () =>
    {
        if (this.AllowInputs())
        {
            this.CheckDataType(this.SelectedInputDataType())

            if (Number.isNaN(this.InputCount()) || this.InputCount() <= this.InputRemainingCount())
            {
                this.InputModuleSet.push(this.CreateInputModule())
            }

            this._onInputModuleSetChanged.dispatch(this, this.InputModuleSet())
        }
        else
        {
            throw new Error("Input modules are disabled.")
        }
    }

    public DeleteInputModule = (value: OneDasModuleViewModel) =>
    {
        this.InputModuleSet.pop()
        this.Update()
        this._onInputModuleSetChanged.dispatch(this, this.InputModuleSet())
    }

    public AddOutputModule = () =>
    {
        if (this.AllowOutputs())
        {
            this.CheckDataType(this.SelectedOutputDataType())

            if (Number.isNaN(this.OutputCount()) || this.OutputCount() <= this.OutputRemainingCount())
            {
                this.OutputModuleSet.push(this.CreateOutputModule())
            }

            this._onOutputModuleSetChanged.dispatch(this, this.OutputModuleSet())
        }
        else
        {
            throw new Error("Outputs modules are disabled.")
        }
    }

    public DeleteOutputModule = (value: OneDasModuleViewModel) =>
    {
        this.OutputModuleSet.pop()
        this.Update()
        this._onOutputModuleSetChanged.dispatch(this, this.OutputModuleSet())
    }

    public CheckDataType(oneDasDataType: OneDasDataTypeEnum)
    {
        if (!this.AllowBoolean() && oneDasDataType === OneDasDataTypeEnum.BOOLEAN)
        {
            throw new Error("Wrong data direction of module.")
        }
    }
}