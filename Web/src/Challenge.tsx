import { useNavigate, useParams } from 'react-router-dom';
import { Avatar, Button, Col, Progress, Row, Table, Tooltip } from 'antd';
import { ArrowLeftOutlined, SyncOutlined } from '@ant-design/icons';
import 'antd/dist/antd.css';
import { get } from 'lodash';
import { AlignType } from 'rc-table/lib/interface';
import './App.css';
import { useCachedApiCallWithReload } from './helpers';
import { ChallengeClient, AthleteChallengeStatus, ChallengeDto } from './restClients';
import { isAndroid } from './MobileCheck';

function Challenge() {
    const params = useParams();
    const [state, reload] = useCachedApiCallWithReload<ChallengeDto>(`challenge_${params.challengeId}`, () => new ChallengeClient().getChallenge(params.challengeId));

    const joinChallenge = async (challengeId: string): Promise<void> => {
        await new ChallengeClient().joinChallenge(challengeId);
        reload();
    }

    const leaveChallenge = async (challengeId: string): Promise<void> => {
        await new ChallengeClient().leaveChallenge(challengeId);
        reload();
    }

    const navigate = useNavigate();
    const navigateToAthleteInfo = (athleteStatus: AthleteChallengeStatus) => {
        navigate(`/challenge/${params.challengeId}/athlete/${athleteStatus.athleteId}`);
    }

    const athleteId = Number(localStorage.getItem('athleteId'));
    const iconSize = isAndroid() ? 24 : 32;

    const columns = [
        {
            title: '#',
            key: 'overall',
            render: (text: string, row: AthleteChallengeStatus, index: number) => <span style={{ cursor: 'pointer' }}>{index + 1}</span>,
            align: 'center' as AlignType,
        },
        {
            title: 'User',
            dataIndex: 'username',
            key: 'username',
            render: (text: string, row: AthleteChallengeStatus, index: number) => <span style={{ cursor: 'pointer' }}><Avatar src={row.avatarUrl} style={{ width: iconSize, height: iconSize, marginRight: '1em' }} /> {text}</span>,
            align: 'left' as AlignType,
        },
        {
            title: get(state.data, 'totalScoreTitle', 'Score'),
            dataIndex: 'totalScore',
            key: 'totalScore',
            render: (text: string, row: AthleteChallengeStatus, index: number) => {
                return (
                    <span style={{ cursor: 'pointer' }}>
                        <span>{row.totalScore}</span>
                        {(row.percentOfGoal || row.percentOfGoal === 0) && <Tooltip title={`${row.percentOfGoal}% of goal`}><Progress type="circle" percent={row.percentOfGoal} width={iconSize} style={{ marginLeft: '1em' }} /></Tooltip>}
                    </span>
                );
            },
            align: 'right' as AlignType,
        },
    ];

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
            <h2>{get(state.data, 'name') || "Challenge"}</h2>
            <Row>
                <Col span={12}><Tooltip title={get(state.data, 'allowedSports', ['-']).join(', ')}><span>Activities: {get(state.data, 'allowedSportsIcons', '-')}</span></Tooltip></Col>
                <Col span={12}><span>Goal: {get(state.data, 'goal', '-')}</span></Col>
            </Row>
            <Table
                style={{marginTop:'1em'}}
                dataSource={get(state.data, 'athletes')}
                columns={columns}
                rowKey='athleteId'
                showHeader={true}
                loading={state.loading}
                pagination={{ position: [] }}
                onRow={(record, index) => {
                    return {
                        onClick: event => navigateToAthleteInfo(record),
                    };
                }} />

            {!state.loading && athleteId &&
                <p style={{ marginTop: '1em' }}>
                    {state.data.athletes.some(a => a.athleteId === athleteId) && <Button type="link" onClick={() => leaveChallenge(params.challengeId)}>Leave the Challenge</Button>}
                    {state.data.athletes.every(a => a.athleteId !== athleteId) && <Button type="link" onClick={() => joinChallenge(params.challengeId)}>Join the Challenge</Button>}
                </p>
            }

            {!state.loading && state.errorMessage && <p style={{ marginTop: '1em', fontSize: 'small' }}>Failed to refresh the challenge data: {state.errorMessage}</p>}
        </div>
    );
}

export default Challenge;