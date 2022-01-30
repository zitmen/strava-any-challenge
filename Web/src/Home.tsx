import moment from 'moment';
import { Fragment } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Carousel, Col, List, Row, Tooltip } from 'antd';
import { UserOutlined, SyncOutlined } from '@ant-design/icons';
import { useCachedApiCallWithReload } from './helpers';
import { ChallengeClient, AllChallenges, ChallengeInfo } from './restClients';

function Home() {
    const [state, reload] = useCachedApiCallWithReload<AllChallenges>('challenges', () => new ChallengeClient().getChallenges());
    const navigate = useNavigate();
    const isAdmin = localStorage.getItem('isAdmin') === "true";
    
    return (
        <div className="App">
            {isAdmin &&
                <span style={{ top: '1em', left: '1em', float: 'left' }}>
                    <Tooltip title="menu">
                        <Button shape="circle" icon={<UserOutlined />} onClick={() => navigate(`/admin`)} style={{ zIndex: 1000 }} />
                    </Tooltip>
                </span>
            }
            <span style={{ top: '1em', right: '1em', float: 'right' }}>
                <Tooltip title="reload">
                    <Button shape="circle" icon={<SyncOutlined />} onClick={reload} style={{ zIndex: 1000 }} />
                </Tooltip>
            </span>

            <Carousel swipeToSlide draggable dots dotPosition="top">
                <Challenges
                    loading={state.loading}
                    data={state.data?.current || []}
                    title="Current Challenges"
                    errorMessage={state.errorMessage}
                />
                <Challenges
                    loading={state.loading}
                    data={state.data?.upcoming || []}
                    title="Upcoming Challenges"
                    errorMessage={state.errorMessage}
                />
                <Challenges
                    loading={state.loading}
                    data={state.data?.past || []}
                    title="Past Challenges"
                    errorMessage={state.errorMessage}
                />
            </Carousel>
        </div>
    );
}

export default Home;

function Challenges({ loading, data, title, errorMessage}) {
    const navigate = useNavigate();
    const renderItem = (item: ChallengeInfo) => renderChallengeInfo(item, () => navigate(`/challenge/${item.id}`));

    return (
        <div>
            <List
                loading={loading}
                dataSource={data}
                header={<h2>{title}</h2>}
                renderItem={renderItem}
            />

            {errorMessage && <p style={{ marginTop: 20, fontSize: 'small' }}>Failed to fetch the challenges: {errorMessage}</p>}
        </div>
    );
}

function renderChallengeInfo(item: ChallengeInfo, navigateToChallenge: () => void) {
    return (
        <Fragment>
            <List.Item key={item.id} style={{ cursor: 'pointer' }} onClick={navigateToChallenge}>
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
        </Fragment>
    );
}

function formatDate(date: Date) {
    return moment(date).format('ll').split(',')[0];
}
