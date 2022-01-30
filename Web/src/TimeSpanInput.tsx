import React, { useState } from 'react';
import { Input } from 'antd';

export interface TimeSpanValue {
    days?: number;
    hours?: number;
    minutes?: number;
    seconds?: number;
}

export interface TimeSpanInputProps {
    value?: TimeSpanValue;
    onChange?: (value: TimeSpanValue) => void;
    style?: any;
}

export function timeSpanInputRequiredValidator(_, timeSpan: TimeSpanValue) {
    return timeSpan?.days + timeSpan?.hours + timeSpan?.minutes + timeSpan?.seconds > 0
        ? Promise.resolve()
        : Promise.reject(new Error('Please set a goal of the challenge'))
}

export default function TimeSpanInput({ value = { days: 0, hours: 0, minutes: 0, seconds: 0 }, onChange, style }: TimeSpanInputProps) {
    const [timeSpan, setTimeSpan] = useState<TimeSpanValue>(value);
    
    function onNumberChange(ev: React.ChangeEvent<HTMLInputElement>, getNewTimeSpan: (x: number) => TimeSpanValue, maxValue?: number) {
        let newNumber = parseInt(ev.target.value, 10);
        if (Number.isNaN(newNumber)) {
            return;
        }
        if (newNumber < 0) {
            newNumber = 0;
        }
        if (maxValue && newNumber > maxValue) {
            newNumber = maxValue;
        }
        const newTimeSpan = getNewTimeSpan(newNumber);
        setTimeSpan(newTimeSpan);
        onChange?.(newTimeSpan);
    }

    return (
        <Input.Group compact style={style}>
            <Input
                style={{ width: '25%', textAlign: 'right' }}
                value={timeSpan.days}
                addonAfter="d"
                onChange={ev => onNumberChange(ev, x => ({ ...timeSpan, days: x }), 30)}
            />
            <Input
                style={{ width: '25%', textAlign: 'right' }}
                value={timeSpan.hours}
                addonAfter="h"
                onChange={ev => onNumberChange(ev, x => ({ ...timeSpan, hours: x }), 23)}
            />
            <Input
                style={{ width: '25%', textAlign: 'right' }}
                value={timeSpan.minutes}
                addonAfter="m"
                onChange={ev => onNumberChange(ev, x => ({ ...timeSpan, minutes: x }), 59)}
            />
            <Input
                style={{ width: '25%', textAlign: 'right' }}
                value={timeSpan.seconds}
                addonAfter="s"
                onChange={ev => onNumberChange(ev, x => ({ ...timeSpan, seconds: x }), 59)}
            />
        </Input.Group>
    );
};
