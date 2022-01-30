import { useNavigate } from "react-router-dom";
import { Button, Tooltip } from "antd";
import { ArrowLeftOutlined } from '@ant-design/icons';
import FormChallengeSettings from "./FormChallengeSettings";
import { ChallengeClient } from "./restClients";

export default function AdminCreateChallenge() {
    const navigate = useNavigate();
    if (localStorage.getItem('isAdmin') !== "true") {
        navigate('/');
    }

    return (
        <div className="App">
            <span style={{ top: '1em', left: '1em', float: 'left' }}>
                <Tooltip title="back">
                    <Button shape="circle" icon={<ArrowLeftOutlined />} onClick={() => window.history.back()} style={{ zIndex: 1000 }} />
                </Tooltip>
            </span>

            <h2>Create Challenge</h2>
            <FormChallengeSettings onSubmit={async (ch, f) => { await new ChallengeClient().createChallenge(ch); f.resetFields(); }} />
        </div>
    );
}
