import { useParams } from 'react-router-dom';
import { Avatar, Button, Col, List, Row, Tooltip } from 'antd';
import { ArrowLeftOutlined, SyncOutlined } from '@ant-design/icons';
import { get } from 'lodash';
import { useCachedApiCallWithReload } from './helpers';
import { ActivityInfo, AthleteChallengeInfo, ChallengeClient } from './restClients';

function Athlete() {
    const params = useParams();

    const [state, reload] = useCachedApiCallWithReload<AthleteChallengeInfo>(
        `challenge_${params.challengeId}_athlete_${params.athleteId}`,
        () => new ChallengeClient().getAthleteChallengeInfo(params.challengeId, params.athleteId));

    return (
        <div className="App">
            <span style={{ top: '1em', left: '1em', float: 'left' }}>
                <Tooltip title="back">
                    <Button shape="circle" icon={<ArrowLeftOutlined />} onClick={() => window.history.back()} style={{ zIndex: 1000 }} />
                </Tooltip>
            </span>
            <span style={{ top: '1em', right: '1em', float: 'right' }}>
                <Tooltip title="reload">
                    <Button shape="circle" icon={<SyncOutlined />} onClick={reload} style={{ zIndex: 1000 }} />
                </Tooltip>
            </span>

            <List
                loading={state.loading}
                dataSource={get(state.data, 'activities', []) as ActivityInfo[]}
                header={<Header athlete={state.data} />}
                renderItem={renderActivity}
            />

            {state.errorMessage && <p style={{ marginTop: 20, fontSize: 'small' }}>Failed to refresh the challenge data: {state.errorMessage}</p>}
        </div>
    );
}

export default Athlete;

function Header(props: { athlete: AthleteChallengeInfo }) {
    if (!props.athlete) return <div />;

    return (
        <div>
            <span style={{ cursor: 'pointer' }} onClick={() => window.open(`https://www.strava.com/athletes/${props.athlete.athleteId}`)}>
                <Avatar src={props.athlete.avatarUrl} style={{ width: 48, height: 48 }} />
            </span>
            <p style={{ margin: 0, padding: 0 }}><strong>{props.athlete.username}</strong></p>
            <p style={{ margin: 0, padding: 0 }}><small>Total distance: {props.athlete.totalDistance.toLocaleString(undefined, { maximumFractionDigits: 0 })}m, {props.athlete.activitiesCount} activities</small></p>
        </div>
    );
}

function renderActivity(item: ActivityInfo) {
    return (
        <List.Item key={item.id} style={{ cursor: 'pointer' }} onClick={() => window.open(`https://www.strava.com/activities/${item.id}`)}>
            <List.Item.Meta
                title={
                    <Row>
                        <Col span={6}>
                            <span style={{
                                fontSize: 10,
                                fontWeight: 'normal',
                                color: 'gray',
                                textAlign: 'left',
                                paddingRight: '1em',
                            }}>
                                {item.startDateLocal}
                            </span>
                        </Col>
                        <Col span={18} style={{ textAlign: 'left', paddingLeft: '1em' }}>{item.name}</Col>
                    </Row>
                }
                description={
                    <Row>
                        <Col span={6}>{item.type}</Col>
                        <Col span={6}>{item.distance}</Col>
                        <Col span={6}>{item.movingTime}</Col>
                        <Col span={6}>{item.kiloJoules}</Col>
                    </Row>
                }
            />
        </List.Item>
    );
}