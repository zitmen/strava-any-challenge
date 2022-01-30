import { useNavigate, useParams } from "react-router-dom";
import { Button, Col, List, Row, Spin, Tooltip } from "antd";
import { ArrowLeftOutlined, SyncOutlined } from '@ant-design/icons';
import FormChallengeSettings from "./FormChallengeSettings";
import { useApiCall, useCachedApiCallWithReload } from "./helpers";
import { AllChallenges, ChallengeClient, ChallengeDto, ChallengeFromUserDto, ChallengeInfo } from "./restClients";
import moment from "moment";

export default function AdminEditChallenge() {
    const navigate = useNavigate();
    if (localStorage.getItem('isAdmin') !== "true") {
        navigate('/');
    }

    const [state, reload] = useCachedApiCallWithReload<AllChallenges>('challenges', () => new ChallengeClient().getChallenges());

    const challenges = state.data.past
        .concat(state.data.current)
        .concat(state.data.upcoming)
        .sort((a, b) => a.from < b.from ? -1 : +1);

    function editItem(item: ChallengeInfo): void {
        navigate(`./${item.id}`);
    }

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

            <h2>Edit Challenge</h2>
            <List
                loading={state.loading}
                itemLayout="horizontal"
                dataSource={challenges}
                style={{ textAlign: 'center' }}
                renderItem={(item) => (
                    <List.Item onClick={() => editItem(item)} key={item.id} style={{ cursor: 'pointer' }}>
                        <List.Item.Meta
                            title={item.name}
                            description={
                                <Row>
                                    <Col span={12}>📅 {formatDate(item.from)} - {formatDate(item.to)}</Col>
                                    <Col span={12}>🎯 {item.goal}</Col>
                                </Row>
                            }
                        />
                    </List.Item>

                )}
            />
        </div>
    );
}

export function AdminEditChallengeForm() {
    const navigate = useNavigate();
    if (localStorage.getItem('isAdmin') !== "true") {
        navigate('/');
    }

    const params = useParams();
    const state = useApiCall<ChallengeDto>(() => new ChallengeClient().getChallenge(params.challengeId));

    return (
        <div className="App">
            <span style={{ top: '1em', left: '1em', float: 'left' }}>
                <Tooltip title="back">
                    <Button shape="circle" icon={<ArrowLeftOutlined />} onClick={() => window.history.back()} style={{ zIndex: 1000 }} />
                </Tooltip>
            </span>

            <h2>Edit Challenge</h2>
            <Spin spinning={state.loading}>
                {!state.loading &&
                    <FormChallengeSettings
                        onSubmit={async (ch, _) => { await new ChallengeClient().editChallenge(params.challengeId, ch); window.history.back(); }}
                        initialValues={mapToChallengeFromUserDto(state.data)}
                    />
                }
            </Spin>
        </div>
    );
}

function formatDate(date: Date) {
    return moment(date).format('ll').split(',')[0];
}

function mapToChallengeFromUserDto(challenge: ChallengeDto): ChallengeFromUserDto {
    const output = {
        name: challenge?.name,
        goalType: challenge?.goalType,
        goal: challenge?.goalRaw,
        allowedSports: challenge?.allowedSports,
        from: challenge?.from,
        to: challenge?.to,
    };
    return output;
}
