﻿declare var moment: any

class AppViewModel
{
    public IsMainViewRequested: KnockoutObservable<boolean>
    public CampaignInfoSet: KnockoutObservableArray<CampaignInfoViewModel>
    public CampaignDescriptionSet: Map<string, string>
    public SelectedDatasetInfoSet: KnockoutObservableArray<DatasetInfoViewModel>
    public SampleRateSet: string[]
    public SelectedSampleRate: KnockoutObservable<string>
    public SelectedFileFormat: KnockoutObservable<FileFormatEnum>
    public SelectedFileGranularity: KnockoutObservable<FileGranularityEnum>
    public IsConnected: KnockoutObservable<boolean>
    public HdfExplorerState: KnockoutObservable<HdfExplorerState>
    public ByteCount: KnockoutObservable<number>
    public Progress: KnockoutObservable<number>
    public Message: KnockoutObservable<string>
    public StartDate: KnockoutObservable<Date>
    public EndDate: KnockoutObservable<Date>   
    public DataAvailabilityStatistics: KnockoutObservable<DataAvailabilityStatisticsViewModel>
    public SelectedCampaignInfo: KnockoutObservable<CampaignInfoViewModel>

    public CanLoadData: KnockoutObservable<boolean>

    private _variableInfoSet: VariableInfoViewModel[]
    private _datasetInfoSet: DatasetInfoViewModel[]
    private _chart: Chart

    constructor(appModel: any)
    {
        let campaignInfoModelSet: any = appModel.CampaignInfoSet;

        this.IsMainViewRequested = ko.observable<boolean>(true)
        this.CampaignDescriptionSet = appModel.CampaignDescriptionSet;
        this.CampaignInfoSet = ko.observableArray(Object.keys(campaignInfoModelSet).map(key => new CampaignInfoViewModel(campaignInfoModelSet[key])));
        this.SelectedDatasetInfoSet = ko.observableArray<DatasetInfoViewModel>()
        this.SelectedSampleRate = ko.observable<string>()
        this.SelectedFileFormat = ko.observable<FileFormatEnum>(FileFormatEnum.CSV)
        this.SelectedFileGranularity = ko.observable<FileGranularityEnum>(FileGranularityEnum.Hour)
        this.IsConnected = ko.observable<boolean>(true)
        this.HdfExplorerState = ko.observable<HdfExplorerState>(appModel.HdfExplorerState)
        this.ByteCount = ko.observable<number>(0)
        this.Progress = ko.observable<number>(0)
        this.Message = ko.observable<string>("")
        this.StartDate = ko.observable<Date>(moment().add(-1, 'days').startOf('day').toDate())
        this.EndDate = ko.observable<Date>(moment().add(0, 'days').startOf('day').toDate())
        this.DataAvailabilityStatistics = ko.observable<DataAvailabilityStatisticsViewModel>()
        this.SelectedCampaignInfo = ko.observable<CampaignInfoViewModel>()

        this.CanLoadData = ko.observable<boolean>(false)

        // enumeration description
        EnumerationHelper.Description["FileFormatEnum_CSV"] = "Comma-separated (*.csv)"
        EnumerationHelper.Description["FileFormatEnum_MAT73"] = "Matlab v7.3 (*.mat)"
        EnumerationHelper.Description["FileFormatEnum_GAM"] = "GAM file (*.gam)"

        EnumerationHelper.Description["FileGranularityEnum_Minute_1"] = "1 file per minute"
        EnumerationHelper.Description["FileGranularityEnum_Minute_10"] = "1 file per 10 minutes"
        EnumerationHelper.Description["FileGranularityEnum_Hour"] = "1 file per hour"
        EnumerationHelper.Description["FileGranularityEnum_Day"] = "1 file per day"

        this._variableInfoSet = MapMany(this.CampaignInfoSet(), campaignInfo => campaignInfo.VariableInfoSet)
        this._datasetInfoSet = MapMany(this._variableInfoSet, variableInfo => variableInfo.DatasetInfoSet)

        this.SampleRateSet = [...new Set(this._datasetInfoSet.map(datasetInfo => datasetInfo.Name.split("_")[0]))].sort((a, b) =>
        {
            switch (true)
            {
                case a.includes('Hz') && !b.includes('Hz'):
                    return -1;
                case !a.includes('Hz') && b.includes('Hz'):
                    return 1;
                case a.includes('Hz') && b.includes('Hz') || !a.includes('Hz') && !b.includes('Hz'):

                    if (a.includes('Hz'))
                    {
                        switch (true)
                        {
                            case parseFloat(a) < parseFloat(b):
                                return 1
                            case parseFloat(a) > parseFloat(b):
                                return -1
                            default:
                                return 0
                        }
                    }
                    else
                    {
                        switch (true)
                        {
                            case parseFloat(a) < parseFloat(b):
                                return -1
                            case parseFloat(a) > parseFloat(b):
                                return 1
                            default:
                                return 0
                        }
                    }
            }
        })

        this.CampaignInfoSet().forEach(campaignInfo => 
        {
            campaignInfo.VariableInfoSet.forEach(variableInfo =>
            {
                variableInfo.DatasetInfoSet.forEach(datasetInfo =>
                {
                    datasetInfo.OnIsSelectedChanged.subscribe((sender, isSelected) => 
                    {
                        this.UpdateSelectedDatainfoSetSet()
                    })
                })
            })
        })

        this.SelectedSampleRate.subscribe(newValue =>
        {
            this._variableInfoSet.forEach(variableInfo => 
            {
                variableInfo.DatasetInfoSet.forEach(datasetInfo =>
                {
                    datasetInfo.IsVisible(datasetInfo.Name.split("_")[0] === this.SelectedSampleRate() && !datasetInfo.Name.endsWith("status"))
                })
            })

            this.UpdateSelectedDatainfoSetSet()
        })

        // validation
        this.StartDate.subscribe(() => this.Validate())
        this.EndDate.subscribe(() => this.Validate())
        this.SelectedSampleRate.subscribe(() => this.Validate())
        this.SelectedDatasetInfoSet.subscribe(() => this.Validate())

        // chart
        this.StartDate.subscribe(async (newValue) =>
        {
            if (!this.IsMainViewRequested())
            {
                this.PrepareChart()
            }
        })

        this.EndDate.subscribe(async (newValue) =>
        {
            if (!this.IsMainViewRequested())
            {
                this.PrepareChart()
            }
        })

        this.IsMainViewRequested.subscribe(newValue =>
        {
            if (!newValue)
            {
                this.PrepareChart()
            }
        })

        this.DataAvailabilityStatistics.subscribe(newValue =>
        {
            if (newValue)
            {
                let context: any

                context = document.getElementById("chart_data_availability");
                this._chart = this.CreateChart(context, newValue)
            }
        })

        // callback
        broadcaster.on("SendState", (hdfExplorerState: HdfExplorerState) =>
        {
            this.HdfExplorerState(hdfExplorerState)
        })

        broadcaster.on("SendProgress", (progress: number, message: string) =>
        {
            this.Progress(progress)
            this.Message(message)
        })

        broadcaster.on("SendByteCount", (byteCount: number) =>
        {
            this.ByteCount(byteCount)
        })

        // jQeuery
        $("#start-date").on("change.datetimepicker", (e: any) =>
        {
            this.StartDate(e.date.toDate())
        })

        $("#end-date").on("change.datetimepicker", (e: any) =>
        {
            this.EndDate(e.date.toDate())
        })
    }  

    // methods
    private Validate()
    {
        this.CanLoadData(
            (this.StartDate().valueOf() < this.EndDate().valueOf()) &&
            this.SelectedDatasetInfoSet().length > 0 &&
            this.SelectedFileGranularity() >= 86400 / this.GetSamplesPerDayFromString(this.SelectedSampleRate())
        )
    }

    private GetSamplesPerDayFromString = (datasetName: string) =>
    {
        if (!datasetName)
        {
            return 0;
        }
        
        if (datasetName.startsWith("100 Hz"))
        {
            return 86400 * 100;
        }
        else if (datasetName.startsWith("25 Hz"))
        {
            return 86400 * 25;
        }
        else if (datasetName.startsWith("5 Hz"))
        {
            return 86400 * 5;
        }
        else if (datasetName.startsWith("1 Hz"))
        {
            return 86400 * 1;
        }
        else if (datasetName.startsWith("1 s"))
        {
            return 86400 * 1;
        }
        else if (datasetName.startsWith("60 s"))
        {
            return 86400 / 60;
        }
        else if (datasetName.startsWith("600 s"))
        {
            return 86400 / 600;
        }
        else
        {
            throw new Error("DatasetName cannot be converted to samples per day.");
        }
    }

    private RemoveTimeZoneOffset = (date: Date) =>
    {
        return moment(date).add(<any>moment(date).utcOffset(), "minute").toDate()
    }

    private PrepareChart = async () =>
    {
        if (this._chart)
        {
            this._chart.destroy()
        }

        try
        {
            this.DataAvailabilityStatistics(await broadcaster.invoke("GetDataAvailabilityStatistics",
                                                                    this.SelectedCampaignInfo().Name,
                                                                    this.RemoveTimeZoneOffset(this.StartDate()),
                                                                    this.RemoveTimeZoneOffset(this.EndDate())))
        } catch (e)
        {
            alert(e.message)
        }
    }

    private CreateChart = (context: any, dataAvailabilityStatistics: DataAvailabilityStatisticsViewModel) =>
    {
        let xLabels: any[]
        let data: any[]
        let dateFormat: string
        let yLabel: string
        let yLimit: number
        let showYAxis: boolean

        xLabels = []
        data = []

        switch (dataAvailabilityStatistics.Granularity)
        {
            case DataAvailabilityGranularityEnum.ChunkLevel:

                for (var i = 0; i < dataAvailabilityStatistics.Data.length; i++)
                {
                    xLabels.push(moment(this.StartDate()).add(i, "minutes"))
                }

                for (var i = 0; i < dataAvailabilityStatistics.Data.length; i++)
                {
                    data.push(dataAvailabilityStatistics.Data[i])
                }

                yLabel = "availability"
                yLimit = 1.3
                showYAxis = false
                dateFormat = "DD.MM  -  HH:mm"

                break

            case DataAvailabilityGranularityEnum.DayLevel:

                for (var i = 0; i < dataAvailabilityStatistics.Data.length; i++)
                {
                    xLabels.push(moment(this.StartDate()).add(i, "days"))
                }

                for (var i = 0; i < dataAvailabilityStatistics.Data.length; i++)
                {
                    data.push(dataAvailabilityStatistics.Data[i])
                }

                yLabel = "availability in %"
                yLimit = 120
                showYAxis = true
                dateFormat = "DD.MM"

                break

            case DataAvailabilityGranularityEnum.MonthLevel:

                for (var i = 0; i < dataAvailabilityStatistics.Data.length; i++)
                {
                    xLabels.push(moment(this.StartDate()).add(i, "months"))
                }

                for (var i = 0; i < dataAvailabilityStatistics.Data.length; i++)
                {
                    data.push(dataAvailabilityStatistics.Data[i])
                }

                yLabel = "availability in %"
                yLimit = 120
                showYAxis = true
                dateFormat = "MMM YYYY"

                break

            default:
                new Error("not supported")
        }

        return new Chart(context,
            {
                type: "line",
                data: {
                    labels: xLabels,
                    datasets: [{
                        data: data,
                        backgroundColor: "rgba(75, 192, 192, 0.1)",
                        borderColor: "rgba(75, 192, 192)",
                        borderWidth: 1,
                        lineTension: 0.25,
                        pointRadius: 0
                    }]
                },
                options: {
                    legend: {
                        display: false
                    },
                    scales: {
                        xAxes: [{
                            display: true,
                            scaleLabel: {
                                display: true,
                                labelString: 'date / time'
                            },
                            ticks: {
                                autoSkip: true,
                                minRotation: 45,
                                maxRotation: 45,
                            },
                            time: {
                                displayFormats: {
                                    "minute": dateFormat,
                                    "hour": dateFormat,
                                    "day": dateFormat,
                                    "month": dateFormat,
                                    "year": dateFormat
                                }
                            },
                            type: "time",
                        }],
                        yAxes: [{
                            display: showYAxis,
                            position: "left",
                            scaleLabel: {
                                display: true,
                                labelString: yLabel
                            },
                            ticks: <any>{
                                beginAtZero: true,
                                max: yLimit
                            },
                            type: "linear"                           
                        }]
                    },
                    title: {
                        display: true,
                        fontColor: "#555",
                        fontSize: 17,
                        fontStyle: "",
                        padding: 25,
                        text: "Data availability of " + this.SelectedCampaignInfo().GetDisplayName()
                    },
                    tooltips: {
                        enabled: true
                    }
                }
            })
    }

    private UpdateSelectedDatainfoSetSet = (() =>
    {
        this.SelectedDatasetInfoSet(this._datasetInfoSet.filter(datasetInfo => datasetInfo.IsVisible() && datasetInfo.IsSelected()))
    })

    // commands
    public ShowDataAvailability = (campaignInfo: CampaignInfoViewModel) =>
    {
        this.SelectedCampaignInfo(campaignInfo)
        this.IsMainViewRequested(false)
    }

    public ShowMainView = () =>
    {
        this.SelectedCampaignInfo(null)
        this.IsMainViewRequested(true)
    }

    public CancelLoadData = () =>
    {
        broadcaster.invoke("CancelGetData")
    }

    public LoadData = async () =>
    {
        let campaignInfoSet: Map<string, Map<string, string[]>>
        let variableInfoSet: Map<string, string[]>
        let datasetInfoSet: string[]
        let downloadLink: string

        campaignInfoSet = new Map<string, Map<string, string[]>>()

        this.SelectedDatasetInfoSet().forEach(datasetInfo =>
        {
            if (campaignInfoSet.has(datasetInfo.Parent.Parent.Name))
            {
                variableInfoSet = campaignInfoSet.get(datasetInfo.Parent.Parent.Name)
            }
            else
            {
                variableInfoSet = new Map<string, string[]>();
                campaignInfoSet.set(datasetInfo.Parent.Parent.Name, variableInfoSet)
            }

            if (variableInfoSet.has(datasetInfo.Parent.Name))
            {
                datasetInfoSet = variableInfoSet.get(datasetInfo.Parent.Name)
            }
            else
            {
                datasetInfoSet = [];
                variableInfoSet.set(datasetInfo.Parent.Name, datasetInfoSet)
            }

            datasetInfoSet.push(datasetInfo.Name)
        })

        try
        {
            this.Progress(0)
            this.Message("")

            downloadLink = await broadcaster.invoke(
                "GetData",
                this.RemoveTimeZoneOffset(this.StartDate()),
                this.RemoveTimeZoneOffset(this.EndDate()),
                this.SelectedSampleRate(),
                this.SelectedFileFormat(),
                this.SelectedFileGranularity(),
                campaignInfoSet
            )

            if (downloadLink !== "")
            {
                window.open(downloadLink);
            }
        } catch (e)
        {
            alert(e.message)
        }
    }
}