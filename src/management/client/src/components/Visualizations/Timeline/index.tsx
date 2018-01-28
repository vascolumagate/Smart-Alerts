// -----------------------------------------------------------------------
// <copyright file="index.tsx" company="Microsoft Corporation">
//        Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

import * as React from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';

import Visualization, { VisualizationProps } from '../VisualizationBase';
import FormatUtils from '../../../utils/FormatUtils';
import TimelineChart from '../../../models/Charts/TimelineChart';

interface TimelineProps extends VisualizationProps {
    timelineChart: TimelineChart;
}

/**
 * This component represents the timeline visualization rendering
 */
export default class Timeline extends Visualization<TimelineProps> {
    public render() {
        const { timelineChart } = this.props;

        return (
            <ResponsiveContainer height={this.props.height}>
                <LineChart 
                           data={timelineChart.data} 
                           margin={{ top: 5, right: 30, left: 20, bottom: 5 }} 
                           className={this.props.className}
                >
                    <XAxis 
                        dataKey={timelineChart.timestampDataKey} 
                        tickFormatter={this.hourFormat} 
                        minTickGap={20} 
                        hide={this.props.hideXAxis}
                    />
                    <YAxis 
                        type="number"
                        tickFormatter={FormatUtils.kmNumber}
                        hide={this.props.hideYAxis} 
                    />
                    <CartesianGrid strokeDasharray="3 3" horizontal={false} vertical={false} />
                    <Tooltip />
                    {
                        !this.props.hideLegend &&
                        <Legend />
                    }
                    {this.createLineElements(timelineChart)}
                </LineChart>
            </ResponsiveContainer>
        );
    }

    /**
     * Create a line element for each series
     * @param data The chart data
     */
    private createLineElements(timelineChartData: TimelineChart): JSX.Element[] {
        var lineElements: JSX.Element[] = [];
        if (timelineChartData.linesDataKeys && timelineChartData.linesDataKeys.length > 0) {
          lineElements = timelineChartData.linesDataKeys.map((line, idx) => {
            return (
              <Line
                stroke="#ff7300"
                key={idx}
                type="monotone"
                dataKey={line}
                dot={false}
              />
            );
          });
        }

        return lineElements;
    }
}