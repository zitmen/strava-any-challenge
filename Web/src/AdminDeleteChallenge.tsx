import { useNavigate } from "react-router-dom";
import { Button, Col, List, Modal, Row, Tooltip } from "antd";
import { ArrowLeftOutlined, ExclamationCircleOutlined, SyncOutlined } from '@ant-design/icons';
import { useCachedApiCallWithReload } from "./helpers";
import { AllChallenges, ChallengeClient } from "./restClients";
import moment from "moment";

export default function AdminDeleteChallenge() {
    const navigate = useNavigate();
    if (localStorage.getItem('isAdmin') !== "true") {
        navigate('/');
    }

    const [state, reload] = useCachedApiCallWithReload<AllChallenges>('challenges', () => new ChallengeClient().getChallenges());

    const challenges = state.data.past
        .concat(state.data.current)
        .concat(state.data.upcoming)
        .sort((a, b) => a.from < b.from ? -1 : +1);

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

            <h2>Delete Challenge</h2>
            <List
                loading={state.loading}
                itemLayout="horizontal"
                dataSource={challenges}
                style={{ textAlign: 'center' }}
                renderItem={(item) => (
                    <List.Item onClick={() => confirmDelete(item, reload)} key={item.id} style={{ cursor: 'pointer' }}>
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

function confirmDelete(item, reload) {
    let deletion = { isDeleting: false, deletionSucceeded: undefined };

    return Modal.confirm({
        title: 'Confirm',
        icon: <ExclamationCircleOutlined />,
        content: `Are you sure you want to permanently delete the challenge '${item.name}'?`,
        okText: 'Delete',
        okType: 'danger',
        cancelText: 'Cancel',
        onOk: async () => {
            deletion = { isDeleting: true, deletionSucceeded: undefined };
            try {
                await new ChallengeClient().deleteChallenge(item.id);
                deletion = { isDeleting: false, deletionSucceeded: true };
            } catch {
                deletion = { isDeleting: false, deletionSucceeded: false };
            }
        },
        afterClose: () => {
            if (deletion.deletionSucceeded === false) {
                Modal.error({
                    title: 'Error',
                    content: 'Failed to delete the challenge',
                    afterClose: reload,
                });
            } else if (deletion.deletionSucceeded === true) {
                reload();
            }
        }
    });
}

function formatDate(date: Date) {
    return moment(date).format('ll').split(',')[0];
}
