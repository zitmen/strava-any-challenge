import { useNavigate } from "react-router-dom";
import { Button, List, Tooltip } from "antd";
import { ArrowLeftOutlined, DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';

export default function Admin() {
    const navigate = useNavigate();
    if (localStorage.getItem('isAdmin') !== "true") {
        navigate('/');
    }
    
    const data = [
        {
            title: "Create a new challenge",
            icon: <PlusOutlined />,
            redirect: "/admin/create"
        },
        {
            title: "Edit a challenge",
            icon: <EditOutlined />,
            redirect: "/admin/edit"
        },
        {
            title: "Delete a challenge",
            icon: <DeleteOutlined />,
            redirect: "/admin/delete"
        },
    ];

    return (
        <div className="App">
            <span style={{ top: '1em', left: '1em', float: 'left' }}>
                <Tooltip title="back">
                    <Button shape="circle" icon={<ArrowLeftOutlined />} onClick={() => window.history.back()} style={{ zIndex: 1000 }} />
                </Tooltip>
            </span>

            <h2>Admin</h2>
            <List
                itemLayout="horizontal"
                dataSource={data}
                style={{ textAlign: 'center' }}
                renderItem={(item) => (
                    <List.Item onClick={() => navigate(item.redirect)}>
                        <List.Item.Meta
                            title={<>{item.icon} {item.title}</>}
                        />
                    </List.Item>
                )}
            />
        </div>
    );
}
