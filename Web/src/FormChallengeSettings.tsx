import { Button, Form, Input, Select, DatePicker, InputNumber, Spin, Modal, FormInstance } from "antd";
import { useState } from "react";
import { ChallengeFromUserDto } from "./restClients";
import TimeSpanInput, { timeSpanInputRequiredValidator, TimeSpanValue } from "./TimeSpanInput";
import moment, { Moment } from "moment";

const { RangePicker } = DatePicker;

export interface ChallengeSettingsFormData {
    name?: string,
    goalType?: string,
    goal?: number | TimeSpanValue,
    allowedSports?: string[],
    dateRange?: Moment[],
}

export interface FormChallengeSettingsProps {
    initialValues?: ChallengeFromUserDto;
    onSubmit?: (challenge: ChallengeFromUserDto, form: FormInstance<any>) => Promise<void>;
}

export default function FormChallengeSettings({ initialValues = {}, onSubmit }: FormChallengeSettingsProps) {
    const [submitting, setSubmitting] = useState(false);
    const [goalSettings, setGoalSettings] = useState(getGoalSettings(initialValues.goalType));
    const [form] = Form.useForm();

    async function onFinish(values: ChallengeSettingsFormData) {
        setSubmitting(true);
        
        try {
            const challenge: ChallengeFromUserDto = {
                name: values.name,
                goalType: values.goalType,
                goal: goalSettings.isTimeGoal ? timeSpanToString(values.goal as TimeSpanValue) : intToString(values.goal as number),
                allowedSports: values.allowedSports,
                from: values.dateRange[0].startOf('day').toDate(),
                to: values.dateRange[1].endOf('day').toDate(),
            };
            await onSubmit(challenge, form);
        } catch (err) {
            console.log(err);
            Modal.error({
                title: 'Error',
                content: 'Failed to submit the challenge!'
            });
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <Spin spinning={submitting}>
            <Form
                form={form}
                onFinish={onFinish}
                labelCol={{ span: 4 }}
                wrapperCol={{ span: 14 }}
                layout="horizontal"
            >
                <Form.Item
                    name="name"
                    rules={[{ required: true, message: 'Please input a name of the challenge' }]}
                    initialValue={initialValues.name}
                >
                    <Input placeholder="Name" />
                </Form.Item>
                <Form.Item
                    name="goalType"
                    rules={[{ required: true, message: 'Please select a type of the challenge' }]}
                    initialValue={initialValues.goalType}
                >
                    <Select placeholder="Challenge type" onChange={(value) => setGoalSettings(getGoalSettings(value))}>
                        <Select.Option value="totalDistance">Total distance</Select.Option>
                        <Select.Option value="totalTime">Total time</Select.Option>
                        <Select.Option value="totalMovingTime">Total moving time</Select.Option>
                        <Select.Option value="totalKiloJoules">Total kJ</Select.Option>
                        <Select.Option value="totalKiloCalories">Total Cal</Select.Option>
                    </Select>
                </Form.Item>
                {goalSettings.isTimeGoal === true &&
                    <Form.Item
                        name="goal"
                        rules={[{ validator: timeSpanInputRequiredValidator }]}
                        initialValue={parseTimeSpan(initialValues.goal)}
                    >
                        <TimeSpanInput style={{ width: '100%' }} />
                    </Form.Item>
                }
                {goalSettings.isTimeGoal === false &&
                    <Form.Item
                        name="goal"
                        rules={[{ required: true, message: 'Please set a goal of the challenge' }]}
                        initialValue={initialValues.goal}
                    >
                        <InputNumber addonAfter={goalSettings.numericGoalUnits} style={{ width: '100%' }} />
                    </Form.Item>
                }
                <Form.Item
                    name="allowedSports"
                    rules={[{ required: true, message: 'Please select allowed activities' }]}
                    initialValue={initialValues.allowedSports}
                >
                    <Select
                        mode="multiple"
                        allowClear
                        style={{ width: '100%' }}
                        placeholder="Allowed activities"
                    >
                        <Select.Option value="AlpineSki">⛷️ Alpine ski</Select.Option>
                        <Select.Option value="BackcountrySki">⛷️ Backcountry ski</Select.Option>
                        <Select.Option value="Canoeing">🛶 Canoeing</Select.Option>
                        <Select.Option value="Crossfit">🏋 Crossfit</Select.Option>
                        <Select.Option value="EBikeRide">🚴 E-bike ride</Select.Option>
                        <Select.Option value="Elliptical">Elliptical</Select.Option>
                        <Select.Option value="Golf">🏌️ Golf</Select.Option>
                        <Select.Option value="Handcycle">Handcycle</Select.Option>
                        <Select.Option value="Hike">🚶 Hike</Select.Option>
                        <Select.Option value="IceSkate">⛸️ Ice skate</Select.Option>
                        <Select.Option value="InlineSkate">Inline skate</Select.Option>
                        <Select.Option value="Kayaking">Kayaking</Select.Option>
                        <Select.Option value="Kitesurf">Kitesurf</Select.Option>
                        <Select.Option value="NordicSki">⛷️ Nordic ski</Select.Option>
                        <Select.Option value="Ride">🚴 Ride</Select.Option>
                        <Select.Option value="RockClimbing">Rock climbing</Select.Option>
                        <Select.Option value="RollerSki">⛷️ Roller ski</Select.Option>
                        <Select.Option value="Rowing">🚣 Rowing</Select.Option>
                        <Select.Option value="Run">🏃 Run</Select.Option>
                        <Select.Option value="Sail">Sail</Select.Option>
                        <Select.Option value="Skateboard">🛹 Skateboard</Select.Option>
                        <Select.Option value="Snowboard">🏂 Snowboard</Select.Option>
                        <Select.Option value="Snowshoe">Snowshoe</Select.Option>
                        <Select.Option value="Soccer">⚽ Soccer</Select.Option>
                        <Select.Option value="StairStepper">Stair stepper</Select.Option>
                        <Select.Option value="StandUpPaddling">Stand-up paddling</Select.Option>
                        <Select.Option value="Surfing">🏄 Surfing</Select.Option>
                        <Select.Option value="Swim">🏊 Swim</Select.Option>
                        <Select.Option value="Velomobile">Velomobile</Select.Option>
                        <Select.Option value="VirtualRide">🚴 Virtual ride</Select.Option>
                        <Select.Option value="VirtualRun">🏃 Virtual run</Select.Option>
                        <Select.Option value="Walk">🚶 Walk</Select.Option>
                        <Select.Option value="WeightTraining">🏋 Weight training</Select.Option>
                        <Select.Option value="Wheelchair">👨‍🦽 Wheelchair</Select.Option>
                        <Select.Option value="Windsurf">Windsurf</Select.Option>
                        <Select.Option value="Workout">🏋 Workout</Select.Option>
                        <Select.Option value="Yoga">🧘 Yoga</Select.Option>
                    </Select>
                </Form.Item>
                <Form.Item
                    name="dateRange"
                    rules={[{ required: true, message: 'Please set the challenge start and end dates' }]}
                    initialValue={[moment(initialValues.from), moment(initialValues.to)]}
                >
                    <RangePicker
                        placeholder={["Challenge start", "Challenge end"]}
                        style={{ width: '100%' }}
                    />
                </Form.Item>
                <Form.Item>
                    <Button type="primary" htmlType="submit" style={{ marginRight: '1em' }}>Submit</Button>
                    <Button htmlType="button" onClick={() => form.resetFields()} style={{ marginLeft: '1em' }}>Reset</Button>
                </Form.Item>
            </Form>
        </Spin>
    );
};

function getGoalSettings(value) {
    switch (value) {
        case 'totalDistance': return { isTimeGoal: false, numericGoalUnits: 'm' };
        case 'totalTime': return { isTimeGoal: true, numericGoalUnits: undefined };
        case 'totalMovingTime': return { isTimeGoal: true, numericGoalUnits: undefined };
        case 'totalKiloJoules': return { isTimeGoal: false, numericGoalUnits: 'kJ' };
        case 'totalKiloCalories': return { isTimeGoal: false, numericGoalUnits: 'Cal' };
        default: return { isTimeGoal: undefined, numericGoalUnits: undefined };
    }
};

function timeSpanToString(timeSpan: TimeSpanValue): string {
    const days = timeSpan.days.toLocaleString('en-US', { useGrouping: false });
    const hours = timeSpan.hours.toLocaleString('en-US', { minimumIntegerDigits: 2, useGrouping: false });
    const minutes = timeSpan.minutes.toLocaleString('en-US', { minimumIntegerDigits: 2, useGrouping: false });
    const seconds = timeSpan.seconds.toLocaleString('en-US', { minimumIntegerDigits: 2, useGrouping: false });
    return `${days}.${hours}:${minutes}:${seconds}`;
}

function intToString(value: number): string {
    return value.toLocaleString('en-US', { useGrouping: false });
}

function parseTimeSpan(goal: string | undefined): TimeSpanValue {
    const matches = (goal || '0.00:00:00').match(/(?<days>\d*)\.?(?<hours>\d+):(?<minutes>\d+):(?<seconds>\d+)/);
    return {
        days: Number(matches.groups.days),
        hours: Number(matches.groups.hours),
        minutes: Number(matches.groups.minutes),
        seconds: Number(matches.groups.seconds),
    };
}
